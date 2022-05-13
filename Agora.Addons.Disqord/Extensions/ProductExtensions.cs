using Disqord;
using Emporia.Domain.Entities;
using Humanizer;

namespace Agora.Addons.Disqord.Extensions
{
    public static class ProductExtensions
    {
        public static LocalEmbed ToEmbed(this Listing listing)
        {
            return new LocalEmbed
            {
                Title = listing.Product.Title.Value,
                Author = listing.UniqueTrait(),
                Description = listing.Product.Description?.Value,
                ImageUrl = listing.Product.Carousel?.Images.FirstOrDefault()?.Url,
                Footer = new LocalEmbedFooter().WithText($"Reference Code: {listing.ReferenceCode}")
            }
            .WithProductDetails(listing);
        }

        private static LocalEmbed WithProductDetails(this LocalEmbed embed, Listing listing) => listing.Product switch
        {
            AuctionItem auction => embed.AddInlineField("Quantity", auction.Quantity.Amount.ToString())
                                        .AddInlineField("Starting Price", auction.StartingPrice.ToString())
                                        .AddInlineField("Current Bid", auction.CurrentPrice.ToString())
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledEnd))
                                        .AddInlineField("Expires In", Markdown.Timestamp(listing.ExpirationDate, Markdown.TimestampFormat.RelativeTime))
                                        .AddField("Item Owner", listing.Anonymous 
                                                                    ? Markdown.BoldItalics("Anonymous") 
                                                                    : Mention.User(listing.Owner.ReferenceNumber.Value)),
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
