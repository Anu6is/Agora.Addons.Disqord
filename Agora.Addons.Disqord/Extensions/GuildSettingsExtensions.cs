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
                if (settings.AllowedListings.Any(listing => listing.Contains("Exchange")))
                    return settings.AvailableRooms.Any(room => room.Equals("Exchange"));

                return true;
            },  "An Exchange room (channel) is required.")
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
                if (settings.AvailableRooms.Any(room => room.Equals("Exchange")))
                    return settings.AllowedListings.Any(listing => listing.Contains("Exchange"));

                return true;
            },  "Exchange room configured but exchange are listings not allowed.")
        };

        public static LocalEmbed ToEmbed(this IDiscordGuildSettings settings, string highlightField = null, LocalEmoji highlighEmoji = null)
        {
            var serverTime = Markdown.Bold(DateTimeOffset.UtcNow.ToOffset(settings.Offset).ToString("MMMM d, yyyy HH:mm  [zzz] "));
            var snipeExtension = settings.SnipeExtension == TimeSpan.Zero ? AgoraEmoji.RedCrossMark : AgoraEmoji.GreenCheckMark;
            var snipeRange = settings.SnipeRange == TimeSpan.Zero ? AgoraEmoji.RedCrossMark : AgoraEmoji.GreenCheckMark;
            var economy = settings.EconomyType.Equals("Disabled") ? AgoraEmoji.RedCrossMark : AgoraEmoji.GreenCheckMark;
            var absenteeBid = settings.AllowAbsenteeBidding ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
            var shillBid = settings.AllowShillBidding ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
            var localTime = Markdown.Timestamp(DateTimeOffset.UtcNow.ToOffset(settings.Offset));
            var missing = settings.FindMissingRequirement();
            var description = missing is null
                ? Markdown.Italics("Select an option from the drop-down list to modify the selected setting.")
                : Markdown.Bold($"💡{Markdown.CodeBlock(missing)}");

            var embed = new LocalEmbed()
                .WithDefaultColor()
                .WithTitle("Server Settings")
                .WithDescription(description)
                .AddField("Server Time", $"{serverTime}{Environment.NewLine}{localTime} **[Local]**")
                .AddField($"{economy} Server Economy", settings.EconomyType)
                .AddField("Default Currency", $"Symbol: **{settings.DefaultCurrency.Symbol}** | Decimals: **{settings.DefaultCurrency.DecimalDigits}** | Format: **{settings.DefaultCurrency}**")
                .AddInlineField("Result Logs", settings.ResultLogChannelId == 0 ? Markdown.Italics("Undefined") : Mention.Channel(new Snowflake(settings.ResultLogChannelId)))
                .AddInlineField("Audit Logs", settings.AuditLogChannelId == 0 ? Markdown.Italics("Undefined") : Mention.Channel(new Snowflake(settings.AuditLogChannelId)))
                .AddInlineBlankField()
                .AddInlineField("Minimum Duration", settings.MinimumDuration.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second))
                .AddInlineField("Maximum Duration", settings.MaximumDuration.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second))
                .AddInlineBlankField()
                .AddInlineField($"{snipeRange} Snipe Trigger", settings.SnipeRange.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second))
                .AddInlineField($"{snipeExtension} Snipe Extension", settings.SnipeExtension.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second))
                .AddInlineBlankField()
                .AddInlineField($"{shillBid} Shill Bidding", settings.AllowShillBidding ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineField($"{absenteeBid} Absentee Bidding", settings.AllowAbsenteeBidding ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineBlankField()
                .AddInlineField("Manager Role", settings.AdminRole == 0 ? Markdown.Italics("Undefined") : Mention.Role(new Snowflake(settings.AdminRole)))
                .AddInlineField("Broker Role", settings.BrokerRole == 0 ? Markdown.Italics("Undefined") : Mention.Role(new Snowflake(settings.BrokerRole)))
                .AddInlineBlankField()
                .AddInlineField("Merchant Role", settings.MerchantRole == 0 ? Mention.Everyone : Mention.Role(new Snowflake(settings.MerchantRole)))
                .AddInlineField("Buyer Role", settings.BuyerRole == 0 ? Mention.Everyone : Mention.Role(new Snowflake(settings.BuyerRole)))
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
            embed.AddField("Exchange", rooms.Contains("Exchange") ? GetRoomDetails(showrooms, ListingType.Exchange) : $"{AgoraEmoji.RedCrossMark}| Exchange listings disabled");

            return embed;
        }

        private static string GetRoomDetails(IEnumerable<Showroom> showrooms, ListingType listingType)
        {
            var rooms = showrooms.Where(x => x.ListingType == listingType.ToString());

            if (rooms.Any())
                return string.Join(Environment.NewLine, rooms.Select(s =>
                {
                    var status = s.IsActive ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;
                    var businessHours = s.ActiveHours == null ? "24-hours" : s.ActiveHours.ToString();
                    var roomDetails = $"{status}|{Mention.Channel(new Snowflake(s.Id.Value))} | {Markdown.Code("Business Hours:")} {Markdown.Bold(businessHours)}";

                    return roomDetails.Length > Discord.Limits.Message.Embed.Field.MaxValueLength
                            ? roomDetails[..Discord.Limits.Message.Embed.Field.MaxValueLength]
                            : roomDetails;
                }));
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
