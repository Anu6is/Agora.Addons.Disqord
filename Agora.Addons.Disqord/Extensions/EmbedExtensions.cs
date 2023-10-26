using Disqord;

namespace Agora.Addons.Disqord.Extensions
{
    public static class EmbedExtensions
    {
        public static LocalEmbed WithDefaultColor(this LocalEmbed e) => e.WithColor(new(0x2F3136));

        public static LocalEmbed AddInlineField(this LocalEmbed e, string name, string value) => e.AddField(name, value, true);

        public static LocalEmbed AddInlineField(this LocalEmbed e, string name, object value) => e.AddField(name, value, true);

        public static LocalEmbed AddInlineField(this LocalEmbed e, LocalEmbedField ef) => e.AddField(ef);

        public static LocalEmbed AddInlineBlankField(this LocalEmbed e) => e.AddBlankField(true);

        public static string GetListingType(this IEmbed e)
        {
            if (e.Title is null) return string.Empty;

            var listing = e.Title.Split(':')[0];

            return listing switch
            {
                { } when listing.Contains("Auction", StringComparison.OrdinalIgnoreCase) => "Auction",
                { } when listing.Contains("Market", StringComparison.OrdinalIgnoreCase) => "Market",
                { } when listing.Contains("Trade", StringComparison.OrdinalIgnoreCase) => "Trade",
                { } when listing.Contains("Giveaway", StringComparison.OrdinalIgnoreCase) => "Giveaway",
                { } when listing.Contains("Raffle", StringComparison.OrdinalIgnoreCase) => "Giveaway",
                _ => string.Empty,
            };
        }
    }
}
