using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Attributes;
using Agora.Shared.Cache;
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
using System.Text;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public sealed class AuditLogService : AgoraService, IAuditLogService
    {
        private readonly DiscordBotBase _agora;
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
            _settingsService = settingsService;
            _commandAccessor = commandAccessor;
            _interactionAccessor = interactionAccessor;
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
                intermediary = $" on behalf of {(productListing.Anonymous ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(owner)}||" : Mention.User(owner))}";
            else
                host = productListing.Anonymous ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(owner)}||" : host;

            var embed = new LocalEmbed().WithDescription($"{host} {Markdown.Underline("listed")} {Markdown.Bold($"{quantity}{title}")}{intermediary} for {original}{Markdown.Bold(value)}")
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledEnd))
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.SteelBlue);

            var id = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(id);
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

            var embed = new LocalEmbed().WithDescription($"{Markdown.Bold($"{quantity}{title}")} hosted by {Mention.User(owner)} has been {Markdown.Underline("withdrawn")} {user}")
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.OrangeRed);

            var id = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(id);
        }

        public async ValueTask<ReferenceNumber> LogOfferSubmittedAsync(Listing productListing, Offer offer)
        {
            await CheckPermissionsAsync(productListing.Owner.EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var title = productListing.Product.Title.ToString();
            var owner = productListing.Anonymous 
                      ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(productListing.Owner.ReferenceNumber.Value)}||" 
                      : Mention.User(productListing.Owner.ReferenceNumber.Value);
            var submitter = offer.UserReference.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var description = new StringBuilder()
                .Append(Mention.User(submitter))
                .Append(" offered ").Append(Markdown.Bold(offer.Submission))
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

            var id = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().WithEmbeds(embeds));

            return ReferenceNumber.Create(id);

        }

        public async ValueTask<ReferenceNumber> LogOfferRevokedAsync(Listing productListing, Offer offer)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var user = string.Empty;
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Anonymous
                      ? $"{Markdown.Italics("Anonymous")} ||{Mention.User(productListing.Owner.ReferenceNumber.Value)}||"
                      : Mention.User(productListing.Owner.ReferenceNumber.Value);
            var submitter = offer.UserReference.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            if (submitter != productListing.User.ReferenceNumber.Value) user = $" by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var description = new StringBuilder()
                .Append("An offer of ").Append(Markdown.Bold(offer.Submission))
                .Append(" made by ").Append(Mention.User(submitter))
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

            var id = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().WithEmbeds(embeds));

            return ReferenceNumber.Create(id);
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

            var localMessage = new LocalMessage().AddEmbed(embed);
            var id = await TrySendMessageAsync(ShowroomId.Value, localMessage);

            return ReferenceNumber.Create(id);
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

            var id = await TrySendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(id);
        }

        private async ValueTask CheckPermissionsAsync(ulong guildId, ulong channelId, Permissions permissions)
        {
            var currentMember = _agora.GetCurrentMember(guildId);
            var channel = _agora.GetChannel(guildId, channelId);

            if (channel == null)
            {
                await ClearChannelSetting();

                throw new NoMatchFoundException($"Unable to verify channel permissions for {Mention.Channel(channelId)}");
            }

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
                        await TrySendMessageAsync(settings.AuditLogChannelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                    else if (settings.ResultLogChannelId != 0)
                        await _agora.SendMessageAsync(settings.ResultLogChannelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                }

                throw new InvalidOperationException(message);
            }

            return;
        }

        private async Task<Snowflake> TrySendMessageAsync(Snowflake channelId, LocalMessage message)
        {
            try
            {
                var msg = await _agora.SendMessageAsync(channelId, message);
                return msg.Id;
            }
            catch (Exception)
            {
                await ClearChannelSetting();
            }

            return 0;
        }

        private async Task ClearChannelSetting()
        {
            var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);

            settings.AuditLogChannelId = 0;

            await (_settingsService as GuildSettingsCacheService).UpdateGuildSettingsAync(settings);
        }
    }
}
