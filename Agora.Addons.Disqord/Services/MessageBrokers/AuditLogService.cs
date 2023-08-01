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
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public sealed class AuditLogService : AgoraService, IAuditLogService
    {
        private readonly DiscordBotBase _agora;
        private readonly ILogger _logger;
        private readonly IGuildSettingsService _settingsService;
        private readonly ICommandContextAccessor _commandAccessor;
        private readonly IInteractionContextAccessor _interactionAccessor;

        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }

        public AuditLogService(DiscordBotBase bot,
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

        public async ValueTask<ReferenceNumber> LogListingCreatedAsync(Listing productListing)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason));

            var intermediary = string.Empty;
            var value = productListing.ValueTag.ToString();
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var host = Mention.User(productListing.User.ReferenceNumber.Value);
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";
            var original = productListing switch
            {
                StandardMarket { Discount: > 0, Product: MarketItem item } => $"{Markdown.Strikethrough(item.Price.ToString())} ",
                FlashMarket { Discount: > 0, Product: MarketItem item } => $"{Markdown.Strikethrough(item.Price.ToString())} ",
                MassMarket { Product: MarketItem item } market => market.CostPerItem.Value * item.Quantity.Amount > item.CurrentPrice
                                                                ? $"{Markdown.Strikethrough(Money.Create(market.CostPerItem.Value * item.Quantity.Amount, item.Price.Currency))} "
                                                                : string.Empty,
                _ => string.Empty
            };

            if (owner != productListing.User.ReferenceNumber.Value)
                intermediary = $" on behalf of {(productListing.Anonymous ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(owner)}||" : Mention.User(owner))}";
            else
                host = productListing.Anonymous ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(owner)}||" : host;

            var embed = new LocalEmbed().WithDescription($"{host} {Markdown.Underline("listed")} {Markdown.Bold($"{quantity}{title}")}{intermediary} for {original}{Markdown.Bold(value)}")
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledEnd))
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.SteelBlue);

            var response = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            if (!response.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, response.FailureReason));

            return ReferenceNumber.Create(response.Data);
        }

        public ValueTask<ReferenceNumber> LogListingUpdatedAsync(Listing productListing)
        {
            throw new NotImplementedException(); //TODO log updates
        }

        public async ValueTask<ReferenceNumber> LogListingWithdrawnAsync(Listing productListing)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason));

            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";
            var user = string.Empty;

            if (owner != productListing.User.ReferenceNumber.Value) user = $"by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var embed = new LocalEmbed().WithDescription($"{Markdown.Bold($"{quantity}{title}")} hosted by {Mention.User(owner)} has been {Markdown.Underline("withdrawn")} {user}")
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.OrangeRed);

            var response = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            if (!response.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, response.FailureReason));

            return ReferenceNumber.Create(response.Data);
        }

        public async ValueTask<ReferenceNumber> LogOfferSubmittedAsync(Listing productListing, Offer offer)
        {
            var result = await CheckPermissionsAsync(productListing.Owner.EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason));

            var title = productListing.Product.Title.ToString();
            var owner = productListing.Anonymous
                      ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(productListing.Owner.ReferenceNumber.Value)}||"
                      : Mention.User(productListing.Owner.ReferenceNumber.Value);
            var submitter = offer.UserReference.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var description = new StringBuilder()
                .Append(Mention.User(submitter))
                .Append(productListing.Product is GiveawayItem ? " acquired " : " offered ").Append(Markdown.Bold(offer.Submission))
                .Append(" for ").Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(owner);

            var embeds = new List<LocalEmbed>()
            {
                new LocalEmbed().WithDescription(description.ToString())
                            .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                            .WithColor(Color.LawnGreen)
            };

            if (offer is Deal tradeOffer && !string.IsNullOrWhiteSpace(tradeOffer.Details))
                embeds.Add(new LocalEmbed().WithDefaultColor().WithDescription($"{Markdown.Bold("Attached Message From")} {Mention.User(submitter)}: {tradeOffer.Details}"));

            var response = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().WithEmbeds(embeds));

            if (!response.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, response.FailureReason));

            return ReferenceNumber.Create(response.Data);

        }

        public async ValueTask<ReferenceNumber> LogOfferRevokedAsync(Listing productListing, Offer offer)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason));

            var user = string.Empty;
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Anonymous
                      ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(productListing.Owner.ReferenceNumber.Value)}||"
                      : Mention.User(productListing.Owner.ReferenceNumber.Value);
            var submitter = offer.UserReference.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            if (submitter != productListing.User.ReferenceNumber.Value) user = $" by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var description = new StringBuilder();

            if (productListing.Product is not GiveawayItem) description.Append("An offer of ");
            
            description.Append(Markdown.Bold(offer.Submission))
                .Append(productListing.Product is GiveawayItem ? " held by " : " made by ").Append(Mention.User(submitter))
                .Append(" for ").Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(owner)
                .Append(" has been ").Append(Markdown.Underline("withdrawn")).Append(user);

            var embeds = new List<LocalEmbed>()
            {
                new LocalEmbed().WithDescription(description.ToString())
                                .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                .WithColor(Color.OrangeRed)
            };

            if (offer is Deal tradeOffer && !string.IsNullOrWhiteSpace(tradeOffer.Details))
                embeds.Add(new LocalEmbed().WithDefaultColor().WithDescription($"{Markdown.Bold("Reason:")} {tradeOffer.Details}"));

            var response = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().WithEmbeds(embeds));

            if (!response.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, response.FailureReason));

            return ReferenceNumber.Create(response.Data);
        }

        public ValueTask<ReferenceNumber> LogOfferAcceptedAsync(Listing productListing, Offer offer)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<ReferenceNumber> LogListingSoldAsync(Listing productListing)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason));

            var title = productListing.Product.Title.ToString();
            var value = productListing.CurrentOffer.Submission.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.UserReference.Value;
            var duration = DateTimeOffset.UtcNow.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var stock = productListing is MassMarket or MultiItemMarket
                ? (productListing.Product as MarketItem).Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
                : productListing.Product.Quantity.Amount;
            var quantity = stock == 1 ? string.Empty : $"{stock} ";

            var description = new StringBuilder()
                .Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner))
                .Append(" was ").Append(Markdown.Underline("claimed"))
                .Append(" after ").Append(duration.Humanize())
                .Append(productListing.Product is GiveawayItem ? " with " : " for ").Append(Markdown.Bold(value))
                .Append(" by ").Append(Mention.User(buyer));

            var embed = new LocalEmbed().WithDescription(description.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.Teal);

            var localMessage = new LocalMessage().AddEmbed(embed);
            var response = await TrySendMessageAsync(ShowroomId.Value, localMessage);

            if (!response.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, response.FailureReason));

            return ReferenceNumber.Create(response.Data);
        }

        public async ValueTask<ReferenceNumber> LogListingExpiredAsync(Listing productListing)
        {
            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason));

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

            var response = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            if (!response.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, response.FailureReason));

            return ReferenceNumber.Create(response.Data);
        }

        private async ValueTask<IResult> CheckPermissionsAsync(ulong guildId, ulong channelId, Permissions permissions)
        {
            var currentMember = _agora.GetCurrentMember(guildId);
            var channel = _agora.GetChannel(guildId, channelId);

            if (channel is null)
            {
                await ClearChannelSetting();
                return Result.Failure($"Unable to verify channel permissions for {Mention.Channel(channelId)}");
            }

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

        private async Task<IResult<Snowflake>> TrySendMessageAsync(Snowflake channelId, LocalMessage message)
        {
            try
            {
                var msg = await _agora.SendMessageAsync(channelId, message);
                return Result.Success(msg.Id);
            }
            catch (Exception ex)
            {
                await ClearChannelSetting();
                return Result<Snowflake>.Exception("An error occured while attempting to update the audit log. Log channel has been disabled", ex);
            }
        }

        private async Task ClearChannelSetting()
        {
            var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);

            settings.AuditLogChannelId = 0;

            await _settingsService.UpdateGuildSettingsAync(settings);
        }
    }
}
