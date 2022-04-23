using Disqord;
using Emporia.Domain.Entities;
using Humanizer;

namespace Agora.Addons.Disqord.Extensions
{
    public static class ProductExtensions
    {
        public static LocalEmbed ToEmbed(this Product product)
        {
            return new LocalEmbed
            {
                Title = product.Title.Value,
                Author = product.Listing.UniqueTrait(),
                Description = product.Description?.Value,
                ImageUrl = product.Carousel?.Images.FirstOrDefault()?.Url,
                Footer = new LocalEmbedFooter().WithText($"Reference Code: {product.Listing.ReferenceCode}")
            }
            .WithProductDetails(product);
        }

        private static LocalEmbed WithProductDetails(this LocalEmbed embed, Product product) => product switch
        {
            AuctionItem auction => embed.AddInlineField("Quantity", auction.Quantity.Amount.ToString())
                                        .AddInlineField("Starting Price", auction.StartingPrice.ToString())
                                        .AddInlineField("Current Bid", auction.ValueTag)
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(auction.Listing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(auction.Listing.ScheduledPeriod.ScheduledEnd))
                                        .AddInlineField("Expires In", Markdown.Timestamp(auction.Listing.ExpiresAt, Markdown.TimestampFormat.RelativeTime))
                                        .AddField("Item Owner", product.Listing.Anonymous 
                                                                    ? Markdown.BoldItalics("Anonymous") 
                                                                    : Mention.User(product.Listing.Owner.ReferenceNumber.Value)),
            _ => embed

        };
        
        private static LocalEmbedAuthor UniqueTrait(this Listing listing) => listing switch
        {
            StandardAuction auction => auction.BuyNowPrice == null ? null : new LocalEmbedAuthor().WithName($"Instant Purchase Price: {auction.BuyNowPrice}"),
            VickreyAuction auction => auction.MaxParticipants == 0 ? null : new LocalEmbedAuthor().WithName($"Max Participants: {auction.MaxParticipants}"),
            LiveAuction auction => auction.Timeout == TimeSpan.Zero ? null : new LocalEmbedAuthor().WithName($"Bidding Timeout: {auction.Timeout.Humanize()}"),
            _ => null
        };
    }
}
