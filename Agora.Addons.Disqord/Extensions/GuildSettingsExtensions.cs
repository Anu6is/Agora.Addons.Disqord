using Disqord;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using Humanizer;
using Humanizer.Localisation;

namespace Agora.Addons.Disqord.Extensions
{
    public static class GuildSettingsExtensions
    {
        private static readonly List<(Func<IDiscordGuildSettings, bool> ValidationCriteria, string Result)> RequiredSettings = new()
        {
            new(settings => settings.ResultLogChannelId != 0ul, "A result log channel is required."),
            new(settings => settings.AllowedListings.Count > 0, "Allowed listings must be configured."),
            new(settings =>
            {
                if (settings.AllowedListings.Any(listing => listing.Contains("Auction")))
                    return settings.AvailableRooms.Any(room => room.Equals("Auction"));

                return true;
            },  "An Auction room (channel) is required."),
            new(settings =>
            {
                if (settings.AllowedListings.Any(listing => listing.Contains("Market")))
                    return settings.AvailableRooms.Any(room => room.Equals("Market"));

                return true;
            },  "A Market room (channel) is required."),
            new(settings =>
            {
                if (settings.AllowedListings.Any(listing => listing.Contains("Trade")))
                    return settings.AvailableRooms.Any(room => room.Equals("Trade"));

                return true;
            },  "A Trade room (channel) is required."),
            new(settings =>
            {
                if (settings.AllowedListings.Any(listing => listing.Contains("Giveaway")))
                    return settings.AvailableRooms.Any(room => room.Equals("Giveaway"));

                return true;
            },  "A Giveaway room (channel) is required.")
        };

        private static readonly List<(Func<IDiscordGuildSettings, bool> ValidationCriteria, string Result)> RoomValidations = new()
        {
            new(settings =>
            {
                if (settings.AvailableRooms.Any(room => room.Equals("Auction")))
                        return settings.AllowedListings.Any(listing => listing.Contains("Auction"));

                return true;
            },  "Auction room configured but auction listings are not allowed."),
            new(settings =>
            {
                if (settings.AvailableRooms.Any(room => room.Equals("Market")))
                    return settings.AllowedListings.Any(listing => listing.Contains("Market"));

                return true;
            }, "Market room configured but market listings are not allowed."),
            new(settings =>
            {
                if (settings.AvailableRooms.Any(room => room.Equals("Trade")))
                    return settings.AllowedListings.Any(listing => listing.Contains("Trade"));

                return true;
            },  "Trade room configured but trade listings are not allowed."),
            new(settings =>
            {
                if (settings.AvailableRooms.Any(room => room.Equals("Giveaway")))
                    return settings.AllowedListings.Any(listing => listing.Contains("Giveaway"));

                return true;
            },  "Giveaway room configured but giveaway listings are not allowed.")
        };

        public static LocalEmbed ToEmbed(this IDiscordGuildSettings settings, string highlightField = null, LocalEmoji highlighEmoji = null)
        {
            var serverTime = Markdown.Bold(DateTimeOffset.UtcNow.ToOffset(settings.Offset).ToString("MMMM d, yyyy HH:mm  [zzz] "));
            var snipeExtension = settings.SnipeExtension == TimeSpan.Zero ? AgoraEmoji.RedCrossMark : AgoraEmoji.GreenCheckMark;
            var snipeRange = settings.SnipeRange == TimeSpan.Zero ? AgoraEmoji.RedCrossMark : AgoraEmoji.GreenCheckMark;
            var bidlimit = settings.BiddingRecallLimit == TimeSpan.Zero ? AgoraEmoji.RedCrossMark : AgoraEmoji.GreenCheckMark;
            var economy = settings.EconomyType.Equals("Disabled") ? AgoraEmoji.RedCrossMark : AgoraEmoji.GreenCheckMark;
            var confirmation = settings.TransactionConfirmation ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
            var shillBid = settings.AllowShillBidding ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
            var listingRecall = settings.AllowListingRecall ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
            var earlyAcceptance = settings.AllowAcceptingOffer ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
            var localTime = Markdown.Timestamp(DateTimeOffset.UtcNow.ToOffset(settings.Offset));
            var minDuration = settings.MinimumDuration.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);
            var maxDuration = settings.MaximumDuration.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);
            var defaultDuration = settings.MinimumDurationDefault ? $"Minimum: {minDuration}" : $"Maximum: {maxDuration}";
            var missing = settings.FindMissingRequirement();
            var description = missing is null
                ? Markdown.Italics("Select an option from the drop-down list to modify the selected setting.")
                : Markdown.Bold($"💡{Markdown.CodeBlock(missing)}");
            var defaultBalance = Money.Create(settings.DefaultBalance == 0 ? settings.DefaultCurrency.MinAmount : settings.DefaultBalance, settings.DefaultCurrency);

            var embed = new LocalEmbed()
                .WithDefaultColor()
                .WithTitle("Server Settings")
                .WithDescription(description)
                .AddField("Server Time", $"{serverTime}{Environment.NewLine}{localTime} **[Local]**")
                .AddField($"{economy} Server Economy", settings.EconomyType)
                .AddField("Default Currency", $"Symbol: **{settings.DefaultCurrency.Symbol}** | Decimals: **{settings.DefaultCurrency.DecimalDigits}** | Format: **{defaultBalance}**")
                .AddInlineField("Result Logs", settings.ResultLogChannelId == 0 ? Markdown.Italics("Undefined") : settings.InlineResults ? Markdown.Bold("Inline Results") : Mention.Channel(new Snowflake(settings.ResultLogChannelId)))
                .AddInlineField("Audit Logs", settings.AuditLogChannelId == 0 ? Markdown.Italics("Undefined") : Mention.Channel(new Snowflake(settings.AuditLogChannelId)))
                .AddInlineBlankField()
                .AddInlineField("Minimum Duration", minDuration)
                .AddInlineField("Maximum Duration", maxDuration)
                .AddInlineField("Default Duration", defaultDuration)
                .AddField($"{earlyAcceptance} Allow Early Acceptance", settings.AllowAcceptingOffer ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineField($"{snipeRange} Snipe Trigger", settings.SnipeRange.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second))
                .AddInlineField($"{snipeExtension} Snipe Extension", settings.SnipeExtension.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second))
                .AddInlineField($"{bidlimit} Bidding Recall Limit", settings.BiddingRecallLimit.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second))
                .AddInlineField($"{shillBid} Shill Bidding", settings.AllowShillBidding ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineField($"{confirmation} Confirm Transactions", settings.TransactionConfirmation ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineField($"{listingRecall} Recall Listings", settings.AllowListingRecall ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineField("Manager Role", settings.AdminRole == 0 || settings.AdminRole == settings.GuildId ? Markdown.Italics("Undefined") : Mention.Role(new Snowflake(settings.AdminRole)))
                .AddInlineField("Broker Role", settings.BrokerRole == 0 || settings.BrokerRole == settings.GuildId ? Markdown.Italics("Undefined") : Mention.Role(new Snowflake(settings.BrokerRole)))
                .AddInlineBlankField()
                .AddInlineField("Merchant Role", settings.MerchantRole == 0 || settings.MerchantRole == settings.GuildId ? Mention.Everyone : Mention.Role(new Snowflake(settings.MerchantRole)))
                .AddInlineField("Buyer Role", settings.BuyerRole == 0 || settings.BuyerRole == settings.GuildId ? Mention.Everyone : Mention.Role(new Snowflake(settings.BuyerRole)))
                .AddInlineBlankField()
                .AddField("Allowed Listings", settings.AllowedListings.Any() ? string.Join(" | ", settings.AllowedListings.Select(setting => Markdown.Bold(setting))) : Markdown.Italics("Undefined"));

            if (highlightField != null)
                embed.Fields.Value.FirstOrDefault(x => x.Name == highlightField)?.WithName($"{highlighEmoji?.ToString() ?? "📝"}{highlightField}");

            return embed;
        }

        public static LocalEmbed ToEmbed(this IDiscordGuildSettings settings, IEnumerable<Showroom> showrooms)
        {
            var missing = settings.GetMissingRequirements();
            var description = missing.IsNull()
                ? Markdown.Italics("Select an option from the drop-down list to modify the selected setting.")
                : Markdown.Bold($"💡{Markdown.CodeBlock(missing)}");
            var rooms = settings.AllowedListings
                                .Select(x => x.Split().Last())
                                .Concat(showrooms.Select(x => x.ListingType.ToString()))
                                .ToHashSet();

            var embed = new LocalEmbed()
                .WithDefaultColor()
                .WithTitle("Server Rooms")
                .WithDescription(description);

            embed.AddField("Auction", rooms.Contains("Auction") ? GetRoomDetails(showrooms, ListingType.Auction) : $"{AgoraEmoji.RedCrossMark}| Auction listings disabled");
            embed.AddField("Market", rooms.Contains("Market") ? GetRoomDetails(showrooms, ListingType.Market) : $"{AgoraEmoji.RedCrossMark}| Market listings disabled");
            embed.AddField("Trade", rooms.Contains("Trade") ? GetRoomDetails(showrooms, ListingType.Trade) : $"{AgoraEmoji.RedCrossMark}| Trade listings disabled");
            embed.AddField("Giveaway", rooms.Contains("Giveaway") ? GetRoomDetails(showrooms, ListingType.Giveaway) : $"{AgoraEmoji.RedCrossMark}| Giveaway listings disabled");
            //embed.AddField("Exchange", rooms.Contains("Exchange") ? GetRoomDetails(showrooms, ListingType.Exchange) : $"{AgoraEmoji.RedCrossMark}| Exchange listings disabled");

            return embed;
        }

        private static string GetRoomDetails(IEnumerable<Showroom> showrooms, ListingType listingType)
        {
            var rooms = showrooms.Where(x => x.ListingType == listingType.ToString());

            if (rooms.Any())
            {
                var details = string.Join(Environment.NewLine, rooms.Select(s =>
                {
                    var status = s.IsActive ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
                    var businessHours = s.ActiveHours == null ? "24-hours" : s.ActiveHours.ToString();
                    var roomDetails = $"{status}|{Mention.Channel(new Snowflake(s.Id.Value))} | {Markdown.Code("Business Hours:")} {Markdown.Bold(businessHours)}";

                    return roomDetails;
                }));

                return details.Length > Discord.Limits.Message.Embed.Field.MaxValueLength
                    ? details[..(Discord.Limits.Message.Embed.Field.MaxValueLength - 5)] + "..."
                    : details;
            }
            else
                return Markdown.Italics("Undefined");
        }

        public static string FindMissingRequirement(this IDiscordGuildSettings settings)
        {
            foreach (var (ValidationCriteria, Result) in RequiredSettings)
                if (!ValidationCriteria(settings))
                    return Result;

            return null;
        }

        public static string GetMissingRequirements(this IDiscordGuildSettings settings)
        {
            var missing = new List<string>();

            foreach (var (ValidationCriteria, Result) in RequiredSettings.Skip(2))
                if (!ValidationCriteria(settings))
                    missing.Add(Result);

            foreach (var (ValidationCriteria, Result) in RoomValidations)
                if (!ValidationCriteria(settings))
                    missing.Add(Result);

            return string.Join(Environment.NewLine, missing);
        }
    }
}
