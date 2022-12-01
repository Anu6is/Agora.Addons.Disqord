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
                if (_interactionAccessor.Context == null
                    || (_interactionAccessor.Context.Interaction is IComponentInteraction component 
                    && component.Message.Id != productListing.Product.ReferenceNumber.Value))
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

                if (channel is IForumChannel forumChannel && (productListing.Status == ListingStatus.Active || productListing.Status == ListingStatus.Locked))
                {
                    forumChannel = await EnsureForumTagsExistAsync(forumChannel, AgoraTag.Active, AgoraTag.Expired, AgoraTag.Sold);

                    var pending = forumChannel.Tags.FirstOrDefault(x => x.Name.Equals("Pending", StringComparison.OrdinalIgnoreCase))?.Id;
                    var active = forumChannel.Tags.FirstOrDefault(x => x.Name.Equals("Active", StringComparison.OrdinalIgnoreCase))?.Id;
                    var thread = (IThreadChannel)_agora.GetChannel(EmporiumId.Value, productListing.Product.ReferenceNumber.Value);

                    if (active != null && !thread.TagIds.Contains(active.Value))
                    {
                        await _agora.ModifyThreadChannelAsync(productListing.Product.ReferenceNumber.Value, x =>
                        {
                            x.TagIds = thread.TagIds.Where(tag => tag != pending.GetValueOrDefault()).Append(active.Value).ToArray();
                        });
                    }
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
                                        Permissions.SendMessages | Permissions.SendEmbeds | 
                                        Permissions.ManageThreads | Permissions.CreatePublicThreads | Permissions.SendMessagesInThreads);

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

                if (productListing.Status != ListingStatus.Withdrawn && showroom is IForumChannel forum)
                {
                    if (_interactionAccessor != null && _interactionAccessor.Context != null)
                    {
                        var interaction = _interactionAccessor.Context.Interaction;

                        if (interaction.Response().HasResponded)
                            await interaction.Followup().SendAsync(new LocalInteractionFollowup().WithContent("Success!").WithIsEphemeral());
                        else
                            await interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Success!").WithIsEphemeral(true));
                    }

                    if (_agora.GetChannel(EmporiumId.Value, channelId) is not CachedThreadChannel post) return;

                    forum = await TagClosedPostAsync(productListing, forum, post);

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

        private async Task<IForumChannel> TagClosedPostAsync(Listing productListing, IForumChannel forum, CachedThreadChannel post)
        {
            forum = await EnsureForumTagsExistAsync(forum, AgoraTag.Expired, AgoraTag.Sold);

            var active = forum.Tags.FirstOrDefault(x => x.Name.Equals("Active", StringComparison.OrdinalIgnoreCase))?.Id;

            if (productListing.Status == ListingStatus.Sold)
            {
                var sold = forum.Tags.First(x => x.Name.Equals("Sold", StringComparison.OrdinalIgnoreCase)).Id;

                if (!post.TagIds.Contains(sold))
                    await _agora.ModifyThreadChannelAsync(productListing.Product.ReferenceNumber.Value, x =>
                    {
                        x.TagIds = post.TagIds.Where(tag => tag != active.GetValueOrDefault()).Append(sold).ToArray();
                    });
            }
            else if (productListing.Status == ListingStatus.Expired)
            {
                var expired = forum.Tags.First(x => x.Name.Equals("Expired", StringComparison.OrdinalIgnoreCase)).Id;

                if (!post.TagIds.Contains(expired))
                    await _agora.ModifyThreadChannelAsync(productListing.Product.ReferenceNumber.Value, x =>
                    {
                        x.TagIds = post.TagIds.Where(tag => tag != active.GetValueOrDefault()).Append(expired).ToArray();
                    });
            }

            return forum;
        }

        public async ValueTask RemoveProductListingAsync(Listing productListing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel) return;

            try
            {
                await _agora.DeleteMessageAsync(ShowroomId.Value, productListing.Product.ReferenceNumber.Value);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to remove product listing {id} in {channel}.", productListing.Product.ReferenceNumber.Value, ShowroomId.Value);
            }

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

        private async ValueTask<ulong> CreateForumPostAsync(IForumChannel forum, LocalMessage message, Listing productListing, string category)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageThreads | Permissions.ManageChannels | Permissions.ManageMessages);

            forum = await EnsureForumTagsExistAsync(forum, AgoraTag.Pending, AgoraTag.Active, AgoraTag.Expired, AgoraTag.Sold);

            var type = productListing.Type.ToString().Replace("Market", "Sale");
            var price = productListing.Type == ListingType.Market ? $"({productListing.ValueTag})" : string.Empty;
            var tags = new List<Snowflake>() { forum.Tags.First(x => x.Name.Equals("Pending", StringComparison.OrdinalIgnoreCase)).Id };

            if (category != string.Empty)
                tags.AddRange(await GetTagIdsAsync(forum, category.Split(':')));

            var showroom = await forum.CreateThreadAsync($"[{type}] {productListing.Product.Title} {price}", message, x =>
            {
                x.AutomaticArchiveDuration = TimeSpan.FromDays(7);
                x.TagIds = tags;
            });

            await showroom.AddMemberAsync(productListing.Owner.ReferenceNumber.Value);
            productListing.SetReference(ReferenceCode.Create($"{productListing.ReferenceCode}:{showroom.Id}"));

            return showroom.Id.RawValue;
        }

        private static async ValueTask<IForumChannel> EnsureForumTagsExistAsync(IForumChannel forum, params LocalForumTag[] tagsToAdd)
        {
            var tagAdded = false;
            var tags = forum.Tags.Select(x => LocalForumTag.CreateFrom(x));

            foreach (var tag in tagsToAdd)
            {
                if (tags.Any(x => x.Name.Value.Equals(tag.Name.Value, StringComparison.OrdinalIgnoreCase))) continue;

                tags = tags.Append(tag);
                tagAdded = true;
            }

            if (tagAdded) 
                return await forum.ModifyAsync(x => x.Tags = tags.ToArray());

            return forum;
        }

        private static async ValueTask<IEnumerable<Snowflake>> GetTagIdsAsync(IForumChannel forum, string[] tagNames)
        {
            var tagAdded = false;
            var tags = forum.Tags.Select(x => LocalForumTag.CreateFrom(x));

            foreach (var name in tagNames)
            {
                if (tags.Any(x => x.Name.Value.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))) continue;
                
                tags = tags.Append(new LocalForumTag() { Emoji = new LocalEmoji("📁"), Name = name.Trim() });
                tagAdded = true;
            }

            if (tagAdded) 
                forum = await forum.ModifyAsync(x => x.Tags = tags.ToArray());

            return forum.Tags.Where(x => tagNames.Any(name => name.Trim().Equals(x.Name))).Select(x => x.Id);
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
