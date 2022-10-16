using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Attributes;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qommon;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public sealed class ProductListingService : AgoraService, IProductListingService
    {
        private readonly DiscordBotBase _agora;
        private readonly IGuildSettingsService _settingsService;
        private readonly ICommandContextAccessor _commandAccessor;
        private readonly IInteractionContextAccessor _interactionAccessor;

        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }

        public ProductListingService(DiscordBotBase bot,
                                        IGuildSettingsService settingsService,
                                        ICommandContextAccessor commandAccessor,
                                        IInteractionContextAccessor interactionAccessor,
                                        ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
            _settingsService = settingsService;
            _commandAccessor = commandAccessor;
            _interactionAccessor = interactionAccessor;
        }

        public async ValueTask<ReferenceNumber> PostProductListingAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value,
                                        ShowroomId.Value,
                                        Permissions.SendMessages | Permissions.SendEmbeds | Permissions.CreatePublicThreads);

            var channelId = ShowroomId.Value;
            var channel = _agora.GetChannel(EmporiumId.Value, channelId);
            var categorization = await GetCategoryAsync(productListing);
            var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);
            var message = new LocalMessage().AddEmbed(productListing.ToEmbed().WithCategory(categorization))
                                            .WithComponents(productListing.Buttons(settings.AllowAcceptingOffer));

            if (channel is CachedForumChannel forum)
                return ReferenceNumber.Create(await CreateForumPostAsync(forum, message, productListing, categorization));

            if (channel is CachedCategoryChannel)
                channelId = await CreateCategoryChannelAsync(productListing);

            var response = await _agora.SendMessageAsync(channelId, message);

            try
            {
                if (channel is ITextChannel textChannel && textChannel.Type == ChannelType.News) await textChannel.CrosspostMessageAsync(response.Id);
            }
            catch (Exception) { }

            if (channelId != ShowroomId.Value) await response.PinAsync();

            return ReferenceNumber.Create(response.Id);
        }

        public async ValueTask<ReferenceNumber> UpdateProductListingAsync(Listing productListing)
        {
            var categorization = await GetCategoryAsync(productListing);
            var productEmbeds = new List<LocalEmbed>() { productListing.ToEmbed().WithCategory(categorization) };

            if (productListing.Product.Carousel.Count > 1) productEmbeds.AddRange(productListing.WithImages());

            var channelId = ShowroomId.Value;
            var channel = _agora.GetChannel(EmporiumId.Value, channelId);
            var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel)
                channelId = productListing.ReferenceCode.Reference();

            try
            {
                if (_interactionAccessor.Context == null)
                {
                    await _agora.ModifyMessageAsync(channelId, productListing.Product.ReferenceNumber.Value, x =>
                    {
                        x.Embeds = productEmbeds;
                        x.Components = productListing.Buttons(settings.AllowAcceptingOffer);
                    });
                }
                else
                {
                    var interaction = _interactionAccessor.Context.Interaction;

                    if (interaction.Response().HasResponded)
                        await interaction.Followup().ModifyResponseAsync(x =>
                        {
                            x.Embeds = productEmbeds;
                            x.Components = productListing.Buttons(settings.AllowAcceptingOffer);
                        });
                    else
                        await interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse()
                        {
                            Embeds = productEmbeds,
                            Components = productListing.Buttons(settings.AllowAcceptingOffer)
                        });
                }

                return productListing.Product.ReferenceNumber;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update product listing: {exception}", ex);
            }

            return null;
        }

        public async ValueTask<ReferenceNumber> OpenBarteringChannelAsync(Listing listing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel) return default;

            await CheckPermissionsAsync(EmporiumId.Value,
                                        ShowroomId.Value,
                                        Permissions.SendMessages | Permissions.SendEmbeds | Permissions.CreatePublicThreads | Permissions.SendMessagesInThreads);

            var duration = listing.ScheduledPeriod.Duration switch
            {
                var minutes when minutes < TimeSpan.FromMinutes(60) => TimeSpan.FromHours(1),
                var hours when hours < TimeSpan.FromHours(24) => TimeSpan.FromDays(1),
                var days when days < TimeSpan.FromDays(3) => TimeSpan.FromDays(3),
                _ => TimeSpan.FromDays(7),
            };

            try
            {
                var product = listing.Product;
                var thread = await _agora.CreatePublicThreadAsync(ShowroomId.Value,
                                                                  $"[{listing.ReferenceCode.Code()}] {product.Title}",
                                                                  product.ReferenceNumber.Value,
                                                                  x => x.AutomaticArchiveDuration = duration);

                await thread.SendMessageAsync(new LocalMessage().WithContent("Execute commands for this item HERE!"));

                return ReferenceNumber.Create(thread.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create bartering channel");
            }

            return default;
        }

        public async ValueTask CloseBarteringChannelAsync(Listing productListing)
        {
            try
            {
                var channelId = productListing.Product.ReferenceNumber.Value;
                var showroom = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

                if (showroom == null) return;

                if (showroom is CachedCategoryChannel or CachedForumChannel)
                    channelId = productListing.ReferenceCode.Reference();

                if (productListing.Status != ListingStatus.Withdrawn && showroom is CachedForumChannel forum)
                {
                    if (_interactionAccessor != null && _interactionAccessor.Context != null)
                    {
                        var interaction = _interactionAccessor.Context.Interaction;

                        if (interaction.Response().HasResponded)
                            await interaction.Followup().SendAsync(new LocalInteractionFollowup().WithContent("Success!").WithIsEphemeral());
                        else
                            await interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Success!").WithIsEphemeral(true));
                    }

                    var post = _agora.GetChannel(EmporiumId.Value, channelId) as CachedThreadChannel;

                    if (post == null) return;

                    await post.ModifyAsync(x =>
                    {
                        x.IsArchived = true;
                        x.IsLocked = true;
                    });
                }
                else
                {
                    var channel = _agora.GetChannel(EmporiumId.Value, channelId);

                    if (channel == null) return;

                    await _agora.DeleteChannelAsync(channelId);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to close bartering channel.");
            }

            return;
        }

        public async ValueTask RemoveProductListingAsync(Listing productListing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel) return;

            await _agora.DeleteMessageAsync(ShowroomId.Value, productListing.Product.ReferenceNumber.Value);
            return;
        }

        private async ValueTask CheckPermissionsAsync(ulong guildId, ulong channelId, Permissions permissions)
        {
            var currentMember = _agora.GetCurrentMember(guildId);
            var channel = _agora.GetChannel(guildId, channelId);

            if (channel == null)
                throw new NoMatchFoundException($"Unable to verify channel permissions for {Mention.Channel(channelId)}");

            var channelPerms = currentMember.CalculateChannelPermissions(channel);

            if (!channelPerms.HasFlag(permissions))
            {
                var message = $"The bot lacks the necessary permissions ({permissions & ~channelPerms}) to post to {Mention.Channel(ShowroomId.Value)}";
                var feedbackId = _interactionAccessor?.Context?.ChannelId ?? _commandAccessor?.Context?.ChannelId;

                if (feedbackId.HasValue && feedbackId != channelId)
                {
                    await _agora.SendMessageAsync(feedbackId.Value, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                }
                else
                {
                    var settings = await _agora.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(guildId);

                    if (settings.AuditLogChannelId != 0)
                        await _agora.SendMessageAsync(settings.AuditLogChannelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                    else if (settings.ResultLogChannelId != 0)
                        await _agora.SendMessageAsync(settings.ResultLogChannelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                }

                throw new InvalidOperationException(message);
            }

            return;
        }

        private async ValueTask<ulong> CreateForumPostAsync(CachedForumChannel forum, LocalMessage message, Listing productListing, string category)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageThreads);

            //TODO: Add tag matching category

            var type = productListing.Type.ToString().Replace("Market", "Sale");
            var price = productListing.Type == ListingType.Market ? $"({productListing.ValueTag})" : string.Empty;
            var showroom = await forum.CreateThreadAsync($"[{type}] {productListing.Product.Title} {price}", message, x =>
            {
                x.AutomaticArchiveDuration = TimeSpan.FromDays(7);
            });

            await showroom.AddMemberAsync(productListing.Owner.ReferenceNumber.Value);
            productListing.SetReference(ReferenceCode.Create($"{productListing.ReferenceCode}:{showroom.Id}"));

            return showroom.Id.RawValue;
        }

        private async ValueTask<ulong> CreateCategoryChannelAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageChannels | Permissions.ManageMessages);
            var showroom = await _agora.CreateTextChannelAsync(EmporiumId.Value,
                                                               productListing.Product.Title.Value,
                                                               x => x.CategoryId = Optional.Create(new Snowflake(ShowroomId.Value)));

            productListing.SetReference(ReferenceCode.Create($"{productListing.ReferenceCode}:{showroom.Id}"));

            return showroom.Id.RawValue;
        }

        private async Task<string> GetCategoryAsync(Listing productListing)
        {
            string categorization = string.Empty;
            var subcategoryId = productListing.Product.SubCategoryId;

            if (subcategoryId != null)
            {
                var emporium = await _agora.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(productListing.Owner.EmporiumId.Value);
                var category = emporium.Categories.FirstOrDefault(c => c.SubCategories.Any(s => s.Id.Equals(subcategoryId)));
                var subcategory = category.SubCategories.FirstOrDefault(s => s.Id.Equals(subcategoryId));

                categorization = $"{category.Title}{(subcategory.Title.Equals(category.Title.Value) ? "" : $": {subcategory.Title}")}";
            }

            return categorization;
        }
    }
}
