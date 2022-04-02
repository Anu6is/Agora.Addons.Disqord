using Disqord;
using Emporia.Extensions.Discord;

namespace Agora.Addons.Disqord.Extensions
{
    public static class GuildSettingsExtensions
    {
        public static LocalEmbed AsEmbed(this IDiscordGuildSettings settings, string highlightField = null, LocalEmoji highlighEmoji = null)
        {
            var serverTime = Markdown.Bold(DateTimeOffset.UtcNow.ToOffset(settings.Offset).ToString("MMMM d, yyyy HH:mm  [zzz] "));
            var localTime = Markdown.Timestamp(DateTimeOffset.UtcNow.ToOffset(settings.Offset));
            var embed = new LocalEmbed()
                .WithDefaultColor()
                .WithTitle("Server Settings")
                .WithDescription(Markdown.Italics("Select an option from the drop-down list to modify the selected setting."))
                .AddField("Server Time", $"{serverTime}{Environment.NewLine}{localTime} **[Local]**" )
                .AddField("Default Currency", $"Symbol: **{settings.DefaultCurrency.Symbol}** | Decimals: **{settings.DefaultCurrency.DecimalDigits}** | Format: **{settings.DefaultCurrency}**")
                .AddInlineField("Snipe Trigger", settings.SnipeRange)
                .AddInlineField("Snipe Extension", settings.SnipeExtension)
                .AddInlineBlankField()
                .AddInlineField("Shill Bidding", settings.AllowShillBidding ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineField("Absentee Bidding", settings.AllowAbsenteeBidding ? Markdown.Bold("Enabled") : Markdown.Italics("Disabled"))
                .AddInlineBlankField()
                .AddInlineField("Result Logs", settings.ResultLogChannelId == 0 ? Markdown.Italics("Undefined") : Mention.Channel(new Snowflake(settings.ResultLogChannelId)))
                .AddInlineField("Audit Logs", settings.AuditLogChannelId == 0 ? Markdown.Italics("Undefined") : Mention.Channel(new Snowflake(settings.AuditLogChannelId)))
                .AddInlineBlankField()
                .AddInlineField("Manager Role", settings.AdminRole == 0 ? Markdown.Italics("Undefined") : Mention.Role(new Snowflake(settings.AdminRole)))
                .AddInlineField("Broker Role", settings.BrokerRole == 0 ? Markdown.Italics("Undefined") : Mention.Role(new Snowflake(settings.BrokerRole)))
                .AddInlineField("Merchant Role", settings.MerchantRole == 0 ? Mention.Everyone : Mention.Role(new Snowflake(settings.MerchantRole)))
                .AddField("Allowed Listings", settings.AllowedListings.Any() ? string.Join(" | ", settings.AllowedListings.Select(setting => Markdown.Bold(setting))) : Markdown.Italics("None"));

            if (highlightField != null)
                embed.Fields.FirstOrDefault(x => x.Name == highlightField)?.WithName($"{highlighEmoji?.ToString() ?? "📝"}{highlightField}");

            return embed;
        }
    }
}
