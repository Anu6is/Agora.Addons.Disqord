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
using System.Text;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class MessageProcessingService : AgoraService, IMessageService, IProductListingService, IAuditLogService, IResultLogService
    {
        private readonly DiscordBotBase _agora;
        private readonly IEmporiaCacheService _emporiaCache;
        private readonly ICommandContextAccessor _commandAccessor;
        private readonly IInteractionContextAccessor _interactionAccessor;

        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }

        public MessageProcessingService(DiscordBotBase bot, 
                                        IEmporiaCacheService emporiaCache,
                                        ICommandContextAccessor commandAccessor,
                                        IInteractionContextAccessor interactionAccessor,
                                        ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
            _emporiaCache = emporiaCache;
            _commandAccessor = commandAccessor;
            _interactionAccessor = interactionAccessor;
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

        public async ValueTask<ReferenceNumber> PostProductListingAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value,
                                        ShowroomId.Value,
                                        Permissions.SendMessages | Permissions.SendEmbeds | Permissions.CreatePublicThreads);

            var channelId = ShowroomId.Value;
            var channel = _agora.GetChannel(EmporiumId.Value, channelId);
            var categorization = await GetCategoryAsync(productListing);
            var message = new LocalMessage().AddEmbed(productListing.ToEmbed().WithCategory(categorization)).WithComponents(productListing.Buttons());

            if (channel is CachedForumChannel forum)
            {
                channelId = await CreateForumPostAsync(forum, message, productListing, categorization);

                return ReferenceNumber.Create(channelId);
            }

            if (channel is CachedCategoryChannel)
                channelId = await CreateCategoryChannelAsync(productListing);
            
            var response = await _agora.SendMessageAsync(channelId, message);

            if (channelId != ShowroomId.Value) await response.PinAsync();

            return ReferenceNumber.Create(response.Id);
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

        public async ValueTask<ReferenceNumber> UpdateProductListingAsync(Listing productListing)
        {
            var categorization = await GetCategoryAsync(productListing);
            var productEmbeds = new List<LocalEmbed>() { productListing.ToEmbed().WithCategory(categorization) };

            if (productListing.Product.Carousel.Count > 1) productEmbeds.AddRange(productListing.WithImages());

            var channelId = ShowroomId.Value;
            var channel = _agora.GetChannel(EmporiumId.Value, channelId);

            if (channel is CachedCategoryChannel or CachedForumChannel)
                channelId = productListing.ReferenceCode.Reference();

            try
            {
                if (_interactionAccessor.Context == null)
                {
                    await _agora.ModifyMessageAsync(channelId,
                        productListing.Product.ReferenceNumber.Value,
                        x =>
                        {
                            x.Embeds = productEmbeds;
                            x.Components = productListing.Buttons();
                        });
                }
                else
                {
                    var interaction = _interactionAccessor.Context.Interaction;

                    if (interaction.Response().HasResponded)
                        await interaction.Followup().ModifyResponseAsync(x =>
                        {
                            x.Embeds = productEmbeds;
                            x.Components = productListing.Buttons();
                        });
                    else
                        await interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse()
                        {
                            Embeds = productEmbeds,
                            Components = productListing.Buttons()
                        });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update product listing: {exception}", ex);
                return null;
            }

            return productListing.Product.ReferenceNumber;
        }

        public async ValueTask<ReferenceNumber> OpenBarteringChannelAsync(Listing listing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel) return default;

            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds | Permissions.CreatePublicThreads | Permissions.SendMessagesInThreads);

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
                return default;
            }
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

                    await post?.ModifyAsync(x => 
                    {
                        x.IsArchived = true;
                        x.IsLocked = true;
                    });
                }
                else
                {
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

        public async ValueTask<ReferenceNumber> LogListingCreatedAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var intermediary = string.Empty;
            var value = productListing.ValueTag.ToString();
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var host = Mention.User(productListing.User.ReferenceNumber.Value);
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";
            var original = productListing switch
            {
                StandardMarket { Discount: > 0, Product: MarketItem item } => $"{Markdown.Strikethrough(item.Price.ToString())} ",
                FlashMarket { Discount: > 0, Product: MarketItem item } market => $"{Markdown.Strikethrough(item.Price.ToString())} ",
                MassMarket { Product: MarketItem item } market => market.CostPerItem.Value * item.Quantity.Amount > item.CurrentPrice
                                                                ? $"{Markdown.Strikethrough(Money.Create(market.CostPerItem.Value * item.Quantity.Amount, item.Price.Currency))} "
                                                                : string.Empty,
                _ => string.Empty
            };

            if (owner != productListing.User.ReferenceNumber.Value)
                intermediary = $" on behalf of {(productListing.Anonymous ? Markdown.Italics("Anonymous") : Mention.User(owner))}";
            else
                host = productListing.Anonymous ? Markdown.Italics("Anonymous") : host;

            var embed = new LocalEmbed().WithDescription($"{host} {Markdown.Underline("listed")} {Markdown.Bold($"{quantity}{title}")}{intermediary} for {original}{Markdown.Bold(value)}")
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledEnd))
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.SteelBlue);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public ValueTask<ReferenceNumber> LogListingUpdatedAsync(Listing productListing)
        {
            throw new NotImplementedException(); //TODO log updates
        }

        public async ValueTask<ReferenceNumber> LogListingWithdrawnAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";
            var user = string.Empty;

            if (owner != productListing.User.ReferenceNumber.Value) user = $"by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var embed = new LocalEmbed().WithDescription($"{Markdown.Bold($"{quantity}{title}")} hosted by {Mention.User(owner)} has been {Markdown.Underline("withrawn")} {user}")
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.OrangeRed);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> LogOfferSubmittedAsync(Listing productListing, Offer offer)
        {
            await CheckPermissionsAsync(productListing.Owner.EmporiumId.Value, productListing.ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var submitter = offer.UserReference.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var description = new StringBuilder()
                .Append(Mention.User(submitter))
                .Append(" offered ").Append(Markdown.Bold(offer.Submission))
                .Append(" for ").Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner));

            var embed = new LocalEmbed().WithDescription(description.ToString())
                            .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                            .WithColor(Color.LawnGreen);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);

        }

        public async ValueTask<ReferenceNumber> LogOfferRevokedAsync(Listing productListing, Offer offer)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var user = string.Empty;
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var submitter = offer.UserReference.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            if (submitter != productListing.User.ReferenceNumber.Value) user = $" by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var description = new StringBuilder()
                .Append("An offer of ").Append(Markdown.Bold(offer.Submission))
                .Append(" made by ").Append(Mention.User(submitter))
                .Append(" for ").Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner))
                .Append(" has been ").Append(Markdown.Underline("withdrawn")).Append(user);

            var embed = new LocalEmbed().WithDescription(description.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.Orange);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public ValueTask<ReferenceNumber> LogOfferAcceptedAsync(Listing productListing, Offer offer)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<ReferenceNumber> LogListingSoldAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var title = productListing.Product.Title.ToString();
            var value = productListing.CurrentOffer.Submission.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.UserReference.Value;
            var duration = DateTimeOffset.UtcNow.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var stock = productListing is MassMarket
                ? (productListing.Product as MarketItem).Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
                : productListing.Product.Quantity.Amount;
            var quantity = stock == 1 ? string.Empty : $"{stock} ";

            var description = new StringBuilder()
                .Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner))
                .Append(" was ").Append(Markdown.Underline("claimed"))
                .Append(" after ").Append(duration.Humanize())
                .Append(" for ").Append(Markdown.Bold(value))
                .Append(" by ").Append(Mention.User(buyer));

            var embed = new LocalEmbed().WithDescription(description.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.Teal);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> LogListingExpiredAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var duration = productListing.ExpirationDate.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var description = new StringBuilder()
                .Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner))
                .Append(" has ").Append(Markdown.Underline("expired"))
                .Append(" after ").Append(duration.Humanize());

            var embed = new LocalEmbed().WithDescription(description.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.SlateGray);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> PostListingSoldAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.UserReference.Value;
            var participants = $"{Mention.User(owner)} | {Mention.User(buyer)}";
            var stock = productListing is MassMarket
                ? (productListing.Product as MarketItem).Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
                : productListing.Product.Quantity.Amount;

            var quantity = stock == 1 ? string.Empty : $"{stock} ";
            var embed = new LocalEmbed().WithTitle($"{productListing} Claimed")
                                        .WithDescription($"{Markdown.Bold($"{quantity}{productListing.Product.Title}")} for {Markdown.Bold(productListing.CurrentOffer.Submission)}")
                                        .WithColor(Color.Teal);

            var carousel = productListing.Product.Carousel;

            if (carousel != null && carousel.Images != null && carousel.Images.Any()) 
                embed.WithThumbnailUrl(carousel.Images[0].Url);

            if (productListing.Product.Description != null) 
                embed.AddField("Description", productListing.Product.Description.Value);

            embed.AddInlineField("Owner", Mention.User(owner)).AddInlineField("Claimed By", Mention.User(buyer));

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().WithContent(participants).AddEmbed(embed));
            var delivered = await SendHiddenMessage(productListing, message);

            if (!delivered)
                await message.ModifyAsync(x =>
                {
                    x.Content = participants;
                    x.Embeds = new[] { embed.WithFooter("The message attached to this listing failed to be delivered.") };
                });

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> PostListingExpiredAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var owner = productListing.Owner.ReferenceNumber.Value;
            var duration = productListing.ExpirationDate.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var embed = new LocalEmbed().WithTitle($"{productListing} Expired")
                                        .WithDescription($"{Markdown.Bold($"{quantity}{productListing.Product.Title}")} @ {Markdown.Bold(productListing.ValueTag)}")
                                        .AddInlineField("Owner", Mention.User(owner)).AddInlineField("Duration", duration.Humanize())
                                        .WithColor(Color.SlateGray);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().WithContent(Mention.User(owner)).AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        private async ValueTask<bool> SendHiddenMessage(Listing productListing, IUserMessage message)
        {
            if (productListing.HiddenMessage == null) return true;

            var guildId = productListing.Owner.EmporiumId;
            var embed = new LocalEmbed().WithTitle("Acquisition includes a message!")
                                        .WithDescription(productListing.HiddenMessage.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.Teal);

            try
            {
                var directChannel = await _agora.CreateDirectChannelAsync(productListing.CurrentOffer.UserReference.Value);
                await directChannel.SendMessageAsync(
                    new LocalMessage()
                        .AddEmbed(embed)
                        .AddComponent(
                            new LocalRowComponent()
                                .WithComponents(
                                    new LocalLinkButtonComponent()
                                        .WithLabel("View Results")
                                        .WithUrl($"https://discordapp.com/channels/{guildId}/{message.ChannelId}/{message.Id}"))));
            }
            catch (Exception)
            {
                return false;
            }

            return true;
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

        public async Task<ulong> SendMesssageAsync(ulong channelId, string message)
        {
            var sentMessage = await _agora.SendMessageAsync(channelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithDefaultColor()));

            return sentMessage.Id;
        }

        public async Task<ulong> SendDirectMessageAsync(ulong userId, string message)
        {
            try
            {
                var directChannel = await _agora.CreateDirectChannelAsync(userId);
                var sentMessage = await directChannel.SendMessageAsync(new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithDefaultColor()));

                return sentMessage.Id;
            }
            catch (Exception)
            {
                return 0;
            }

        }

        public string GetMessageUrl(ulong guildId, ulong channelId, ulong messageId) => $"https://discordapp.com/channels/{guildId}/{channelId}/{messageId}";
    }
}