using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Attributes;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Http;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qommon;
using System.Reflection.Metadata.Ecma335;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public sealed class ProductListingService : AgoraService, IProductListingService
    {
        private readonly DiscordBotBase _agora;
        private readonly ILogger _logger;
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
            _logger = logger;
            _settingsService = settingsService;
            _commandAccessor = commandAccessor;
            _interactionAccessor = interactionAccessor;
        }

        public async ValueTask<ReferenceNumber> PostProductListingAsync(Listing productListing)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value,
                                                     ShowroomId.Value,
                                                     Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful)
            {
                var id = await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason);

                return id == 0 ? null : ReferenceNumber.Create(id);
            }

            var channelId = ShowroomId.Value;
            var channel = _agora.GetChannel(EmporiumId.Value, channelId);

            if (channel is null)
            {
                var id = await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, $"Unable to post to {Mention.Channel(channelId)}");

                return id == 0 ? null : ReferenceNumber.Create(id);
            }

            var categorization = await GetCategoryAsync(productListing);
            var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);
            var hideMinButton = productListing.Product is AuctionItem && settings.Features.HideMinMaxButtons;
            var message = new LocalMessage().AddEmbed(productListing.ToEmbed().WithCategory(categorization))
                                            .WithComponents(productListing.Buttons(settings.Features.AcceptOffers, hideMinButton));

            if (channel is CachedForumChannel forum)
            {
                var id = await CreateForumPostAsync(forum, message, productListing, categorization);

                return id == 0 ? null : ReferenceNumber.Create(id);
            }

            if (channel is CachedCategoryChannel)
            {
                var category = await CreateCategoryChannelAsync(productListing);

                if (!category.IsSuccessful)
                {
                    var id = await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, category.FailureReason);

                    return id == 0 ? null : ReferenceNumber.Create(id);
                }

                channelId = category.Data;
            }

            var response = await _agora.SendMessageAsync(channelId, message);

            result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageMessages);
           
            try
            {
                if (channel is ITextChannel textChannel && textChannel.Type == ChannelType.News)
                {
                    if (!result.IsSuccessful)
                    {
                        var id = await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason);

                        return id == 0 ? null : ReferenceNumber.Create(id);
                    }

                    await textChannel.CrosspostMessageAsync(response.Id);
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Failed to complete news channel publishing");
            }

            if (result.IsSuccessful && channelId != ShowroomId.Value) await response.PinAsync();

            return ReferenceNumber.Create(response.Id);
        }

        public async ValueTask<ReferenceNumber> UpdateProductListingAsync(Listing productListing, bool refreshMessage = true)
        {
            var categorization = await GetCategoryAsync(productListing);
            var productEmbeds = new List<LocalEmbed>() { productListing.ToEmbed().WithCategory(categorization) };

            if (productListing.Product.Carousel.Count > 1) productEmbeds.AddRange(productListing.WithImages());

            var channelId = ShowroomId.Value;
            var channel = _agora.GetChannel(EmporiumId.Value, channelId);

            if (channel is null)
            {
                var id = await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, $"Unable to post to {Mention.Channel(channelId)}");

                return id == 0 ? null : ReferenceNumber.Create(id);
            }

            var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel) channelId = productListing.ReferenceCode.Reference();

            try
            {
                if (refreshMessage) await RefreshProductListingAsync(productListing, productEmbeds, channelId, settings);

                var status = productListing.Status;
                var updateTag =  status == ListingStatus.Active || status == ListingStatus.Locked || status == ListingStatus.Sold;

                if (channel is IForumChannel forumChannel && updateTag) forumChannel = await UpdateForumTagAsync(productListing, forumChannel);

                return productListing.Product.ReferenceNumber;
            }
            catch (RestApiException api) when (api.StatusCode == HttpResponseStatusCode.NotFound)
            {
                //ignore these, the message doesn't exist anymore
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update product listing: {exception}", ex);
            }

            return null;
        }

        private async Task<IForumChannel> UpdateForumTagAsync(Listing productListing, IForumChannel forumChannel)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageThreads);

            if (!result.IsSuccessful) return forumChannel;

            forumChannel = await EnsureForumTagsExistAsync(forumChannel, AgoraTag.Active, AgoraTag.Expired, AgoraTag.Locked, AgoraTag.Sold, AgoraTag.Soon);

            var pending = forumChannel.Tags.FirstOrDefault(x => x.Name.Equals("Pending", StringComparison.OrdinalIgnoreCase))?.Id;
            var active = forumChannel.Tags.FirstOrDefault(x => x.Name.Equals("Active", StringComparison.OrdinalIgnoreCase))?.Id;
            var locked = forumChannel.Tags.FirstOrDefault(x => x.Name.Equals("Locked", StringComparison.OrdinalIgnoreCase))?.Id;
            var soon = forumChannel.Tags.FirstOrDefault(x => x.Name.Equals("Ending Soon", StringComparison.OrdinalIgnoreCase))?.Id;
            var thread = (IThreadChannel)_agora.GetChannel(EmporiumId.Value, productListing.Product.ReferenceNumber.Value);

            try
            {
                if (productListing.Status != ListingStatus.Sold)
                {
                    var isActive = productListing.ScheduledPeriod.ScheduledStart <= SystemClock.Now && active is not null;
                    var isSoon = productListing.ExpiresIn <= TimeSpan.FromHours(1) && soon is not null;

                    if (isActive)
                    {
                        var tagIds = thread.TagIds.Where(tag => tag != pending.GetValueOrDefault() && tag != locked.GetValueOrDefault()).ToList();

                        var count = tagIds.Count;

                        if (!tagIds.Contains(active.Value)) tagIds.Add(active.Value);
                        
                        if (isSoon && !tagIds.Contains(soon.Value)) tagIds.Add(soon.Value);

                        if (count != tagIds.Count) await ModifyThreadTagsAsync(thread, tagIds);
                    }
                    
                    if (!isSoon && soon is not null && thread.TagIds.Contains(soon.Value))
                        await ModifyThreadTagsAsync(thread, thread.TagIds.Where(tag => tag != pending.GetValueOrDefault() && tag != soon.GetValueOrDefault() && tag != locked.GetValueOrDefault()).ToList());
                }
                else if (locked != null)
                {
                    await ModifyThreadTagsAsync(thread, thread.TagIds.Where(tag => tag != pending.GetValueOrDefault() && tag != active.GetValueOrDefault() && tag != soon.GetValueOrDefault()).Append(locked.Value).ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tags on forum post");
            }

            return forumChannel;
        }

        private async Task ModifyThreadTagsAsync(IThreadChannel thread, List<Snowflake> tagIds) => await _agora.ModifyThreadChannelAsync(thread.Id, x =>
        {
            x.TagIds = tagIds.Distinct().ToArray();
        });

        private async Task RefreshProductListingAsync(Listing productListing, List<LocalEmbed> productEmbeds, ulong channelId, IDiscordGuildSettings settings)
        {
            var content = string.Empty;
            var status = productListing.Status;
            var channel = _agora.GetChannel(EmporiumId.Value, channelId);
            var updateTag = status == ListingStatus.Active || status == ListingStatus.Locked || status == ListingStatus.Sold;

            if (channel is IThreadChannel forumChannel && updateTag)
            {
                var expiration = Markdown.Timestamp(productListing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime);

                content = $"Expiration: {expiration}\n";

                content += productListing switch
                {
                    { Type: ListingType.Market } => $"Price: {productListing.ValueTag}",
                    VickreyAuction { Product: AuctionItem item } => $"Bids: {item.Offers.Count}",
                    { Product: AuctionItem item } => $"Current Bid: {(item.Offers.Count == 0 ? "None" : productListing.ValueTag)}",
                    CommissionTrade trade => $"Commission: {trade.ValueTag}",
                    RaffleGiveaway { Product: GiveawayItem item } => item.MaxParticipants == 0 
                        ? $"Ticket Price: {item.TicketPrice}" 
                        : $"{item.Offers?.Count}/{item.MaxParticipants} Tickets | Price: {item.TicketPrice}",
                    StandardGiveaway { Product: GiveawayItem item } => item.MaxParticipants == 0 
                        ? string.Empty 
                        : $"{item.Offers?.Count}/{item.MaxParticipants} Participants",
                    _ => string.Empty
                };
            }

            var hideMinButton = productListing.Product is AuctionItem && settings.Features.HideMinMaxButtons;

            if (_interactionAccessor.Context == null 
                || (_interactionAccessor.Context.Interaction is IComponentInteraction component && component.Message.Id != productListing.Product.ReferenceNumber.Value))
            {
                await _agora.ModifyMessageAsync(channelId, productListing.Product.ReferenceNumber.Value, x =>
                {
                    x.Content = content;
                    x.Embeds = productEmbeds;
                    x.Components = productListing.Buttons(settings.Features.AcceptOffers, hideMinButton);
                });
            }
            else
            {
                await _interactionAccessor.Context.Interaction.ModifyMessageAsync(new LocalInteractionMessageResponse()
                {
                    Content = content,
                    Embeds = productEmbeds,
                    Components = productListing.Buttons(settings.Features.AcceptOffers, hideMinButton)
                });
            }
        }

        public async ValueTask<ReferenceNumber> OpenBarteringChannelAsync(Listing listing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel or CachedStageChannel) return default;

            var result = await CheckPermissionsAsync(EmporiumId.Value,
                                                     ShowroomId.Value,
                                                     Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds | Permissions.ReadMessageHistory |
                                                     Permissions.ManageThreads | Permissions.CreatePublicThreads | Permissions.SendMessagesInThreads);

            if (!result.IsSuccessful)
            {
                var id = await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason);

                return id == 0 ? null : ReferenceNumber.Create(id);
            }

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
            var channelId = productListing.Product.ReferenceNumber.Value;
            var showroom = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (showroom == null) return;

            if (showroom is CachedCategoryChannel or CachedForumChannel or CachedStageChannel)
                channelId = productListing.ReferenceCode.Reference();

            var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);
            
            try
            {
                if (productListing.Status != ListingStatus.Withdrawn && showroom is IForumChannel forum)
                {
                    if (_interactionAccessor != null && _interactionAccessor.Context != null)
                        await _interactionAccessor.Context.Interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Transaction Closed!"));

                    if (_agora.GetChannel(EmporiumId.Value, channelId) is not CachedThreadChannel post) return;

                    await TagClosedPostAsync(productListing, forum, post);

                    if (settings.InlineResults) return;

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

                    if (showroom is ICategoryChannel && settings.InlineResults && productListing.Status > ListingStatus.Withdrawn)
                    {
                        var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageMessages);

                        if (result.IsSuccessful)
                            await _agora.DeleteMessageAsync(channel.Id, productListing.Product.ReferenceNumber.Value);
                        else 
                            await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason);
                    }
                    else
                    {
                        var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageChannels);

                        if (result.IsSuccessful)
                            await _agora.DeleteChannelAsync(channelId);
                        else 
                            await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason);
                    }
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
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageThreads);

            if (!result.IsSuccessful) return forum;

            forum = await EnsureForumTagsExistAsync(forum, AgoraTag.Expired, AgoraTag.Locked, AgoraTag.Sold);

            var soon = forum.Tags.FirstOrDefault(x => x.Name.Equals("Ending Soon", StringComparison.OrdinalIgnoreCase))?.Id;
            var active = forum.Tags.FirstOrDefault(x => x.Name.Equals("Active", StringComparison.OrdinalIgnoreCase))?.Id;
            var locked = forum.Tags.FirstOrDefault(x => x.Name.Equals("Locked", StringComparison.OrdinalIgnoreCase))?.Id;

            if (productListing.Status == ListingStatus.Sold)
            {
                var sold = forum.Tags.FirstOrDefault(x => x.Name.Equals("Sold", StringComparison.OrdinalIgnoreCase))?.Id;

                if (sold.HasValue && !post.TagIds.Contains(sold.Value))
                    await _agora.ModifyThreadChannelAsync(productListing.Product.ReferenceNumber.Value, x =>
                    {
                        x.TagIds = post.TagIds.Where(tag => tag != active.GetValueOrDefault() && tag != soon.GetValueOrDefault() && tag != locked.GetValueOrDefault()).Append(sold.Value).ToArray();
                    });
            }
            else if (productListing.Status == ListingStatus.Expired)
            {
                var expired = forum.Tags.FirstOrDefault(x => x.Name.Equals("Expired", StringComparison.OrdinalIgnoreCase))?.Id;

                if (expired.HasValue && !post.TagIds.Contains(expired.Value))
                    await _agora.ModifyThreadChannelAsync(productListing.Product.ReferenceNumber.Value, x =>
                    {
                        x.TagIds = post.TagIds.Where(tag => tag != active.GetValueOrDefault() && tag != soon.GetValueOrDefault() && tag != locked.GetValueOrDefault()).Append(expired.Value).ToArray();
                    });
            }

            return forum;
        }

        public async ValueTask RemoveProductListingAsync(Listing productListing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel or null) return;

            try
            {
                var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageMessages);

                if (result.IsSuccessful)
                    await _agora.DeleteMessageAsync(ShowroomId.Value, productListing.Product.ReferenceNumber.Value);
                else
                    await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to remove product listing {id} in {channel}.", productListing.Product.ReferenceNumber.Value, ShowroomId.Value);
            }

            return;
        }

        private ValueTask<IResult> CheckPermissionsAsync(ulong guildId, ulong channelId, Permissions permissions)
        {
            var currentMember = _agora.GetCurrentMember(guildId);
            var channel = _agora.GetChannel(guildId, channelId);

            if (channel is null)
                return Result.Failure($"Unable to verify channel permissions for {Mention.Channel(channelId)}");

            var channelPerms = currentMember.CalculateChannelPermissions(channel);

            if (channelPerms.HasFlag(permissions)) return Result.Success();

            var message = $"The bot lacks the necessary permissions ({permissions & ~channelPerms}) to post to {Mention.Channel(ShowroomId.Value)}";
            
            return Result.Failure(message);
        }

        private async Task<ulong> GetFeedbackChannelAsync(ulong guildId, ulong channelId)
        {
            var feedbackId = _interactionAccessor?.Context?.ChannelId ?? _commandAccessor?.Context?.ChannelId;

            if (feedbackId.HasValue && feedbackId != channelId) return feedbackId.Value;
            
            var settings = await _agora.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(guildId);

            if (settings.AuditLogChannelId != 0) return settings.AuditLogChannelId;
            else if (settings.ResultLogChannelId > 1) return settings.ResultLogChannelId;

            return 0;
        }

        private async Task<ulong> TrySendFeedbackAsync(ulong guildId, ulong channelId, string message)
        {
            var interaction = _interactionAccessor?.Context?.Interaction;
            var embed = new LocalEmbed().WithDescription(message).WithColor(Color.Red);

            _logger.LogDebug("Action failure feedback: {message}", message);

            try
            {
                if (interaction is null || interaction is IComponentInteraction)
                {
                    var feedbackId = await GetFeedbackChannelAsync(guildId, channelId);

                    if (feedbackId == 0) return feedbackId;
                    
                    var response = await _agora.SendMessageAsync(feedbackId, new LocalMessage().AddEmbed(embed));
                    return response.Id;
                }
                else
                {
                    await interaction.SendMessageAsync(new LocalInteractionMessageResponse().AddEmbed(embed));
                    return interaction.Id;
                }
            }
            catch (Exception)
            {
                //unable to notify the user
            }
         
            return 0;
        }

        private async ValueTask<ulong> CreateForumPostAsync(IForumChannel forum, LocalMessage message, Listing productListing, string category)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageThreads | Permissions.SendMessagesInThreads | Permissions.ManageChannels);

            if (!result.IsSuccessful)
                return await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason);

            forum = await EnsureForumTagsExistAsync(forum, AgoraTag.Pending, AgoraTag.Active, AgoraTag.Soon, AgoraTag.Expired, AgoraTag.Sold);

            var type = productListing.Type.ToString().Replace("Market", "Sale");
            var tags = new List<Snowflake>();

            var pendingTag = forum.Tags.FirstOrDefault(x => x.Name.Equals("Pending", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
            var activeTag = forum.Tags.FirstOrDefault(x => x.Name.Equals("Active", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
            var endingSoonTag = forum.Tags.FirstOrDefault(x => x.Name.Equals("Ending Soon", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

            if (productListing.ScheduledPeriod.ScheduledStart >= SystemClock.Now.AddSeconds(5))
            {
                tags.Add(pendingTag);
            }
            else
            {
                tags.Add(activeTag);

                if (productListing.ExpiresIn <= TimeSpan.FromHours(1)) tags.Add(endingSoonTag);
            }

            if (category != string.Empty)
                tags.AddRange(await GetTagIdsAsync(forum, category.Split(':')));

            var expiration = Markdown.Timestamp(productListing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime);

            message.WithContent($"Expiration: {expiration}\n");

            message.Content += productListing switch
            {
                { Type: ListingType.Market } => $"Price: {productListing.ValueTag}",
                VickreyAuction { Product: AuctionItem item } => $"Bids: {item.Offers.Count}",
                { Product: AuctionItem item } => $"Current Bid: {(item.Offers.Count == 0 ? "None" : productListing.ValueTag)}",
                CommissionTrade trade => $"Commission: {trade.ValueTag}",
                RaffleGiveaway { Product: GiveawayItem item } => $"Ticket Price: {item.TicketPrice}",
                _ => string.Empty
            };

            var showroom = await forum.CreateThreadAsync($"[{type}] {productListing.Product.Title}", message, x =>
            {
                x.AutomaticArchiveDuration = TimeSpan.FromDays(7);
                x.TagIds = tags.Take(20).ToArray();
            });

            try
            {
                await showroom.AddMemberAsync(productListing.Owner.ReferenceNumber.Value);
            }
            catch (Exception)
            {
                //Failed to subscribe to the owner to the post
            }

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
                return await forum.ModifyAsync(x => x.Tags = tags.Take(20).ToArray());

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
                forum = await forum.ModifyAsync(x => x.Tags = tags.Take(20).ToArray());

            return forum.Tags.Where(x => tagNames.Any(name => name.Trim().Equals(x.Name))).Select(x => x.Id);
        }

        private async ValueTask<IResult<ulong>> CreateCategoryChannelAsync(Listing productListing)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ManageChannels | Permissions.ManageMessages);

            if (!result.IsSuccessful) return Result<ulong>.Failure(result.FailureReason);

            var showroom = await _agora.CreateTextChannelAsync(EmporiumId.Value,
                                                               productListing.Product.Title.Value,
                                                               x => x.CategoryId = Optional.Create(new Snowflake(ShowroomId.Value)));

            productListing.SetReference(ReferenceCode.Create($"{productListing.ReferenceCode}:{showroom.Id}"));

            return Result.Success(showroom.Id.RawValue);
        }

        private async Task<string> GetCategoryAsync(Listing productListing)
        {
            var subcategoryId = productListing.Product?.SubCategoryId;

            if (subcategoryId == null) return string.Empty;

            var emporium = await _agora.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(productListing.Owner.EmporiumId.Value);
            var category = emporium.Categories.FirstOrDefault(c => c.SubCategories.Any(s => s.Id.Equals(subcategoryId)));
            var subcategory = category.SubCategories.FirstOrDefault(s => s.Id.Equals(subcategoryId));

            return $"{category.Title}{(subcategory.Title.Equals(category.Title.Value) ? "" : $": {subcategory.Title}")}";
        }

        public async ValueTask NotifyPendingListingAsync(Listing productListing)
        {
            var channelReference = productListing.ReferenceCode.Reference();
            var channelId = channelReference == 0 ? productListing.Product.ReferenceNumber.Value : channelReference;
            var messageId = productListing.Product.ReferenceNumber.Value;

            var offer = productListing.CurrentOffer;
            var showroom = (IMessageChannel)_agora.GetChannel(EmporiumId.Value, channelId);
            var prompt = $"Action required for pending [transaction]({Discord.MessageJumpLink(EmporiumId.Value, channelId, messageId)})";
            var submission = $"Review offer submitted by {Mention.User(offer.UserReference.Value)} -> {Markdown.Bold(offer.Submission)}.";

            await showroom.SendMessageAsync(
                new LocalMessage()
                    .WithContent(Mention.User(productListing.Owner.ReferenceNumber.Value))
                    .AddEmbed(
                        new LocalEmbed()
                            .WithDefaultColor()
                            .WithDescription(prompt + Environment.NewLine + submission)));

            return;
        }
    }
}
