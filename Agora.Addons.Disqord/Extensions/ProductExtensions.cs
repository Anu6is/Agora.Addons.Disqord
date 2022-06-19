using Disqord;
using Emporia.Domain.Common;
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
            .WithProductDetails(listing)
            .WithDefaultColor();
        }

        public static LocalRowComponent[] Buttons(this Listing listing)
        {
            var type = listing.Type.ToString();
            var edit = LocalComponent.Button($"edit{type}", "Edit").WithStyle(LocalButtonComponentStyle.Primary);
            var extend = LocalComponent.Button($"extend{type}", "Extend").WithStyle(LocalButtonComponentStyle.Primary);
            var withdraw = LocalComponent.Button($"withdraw{type}", "Withdraw").WithStyle(LocalButtonComponentStyle.Danger);
            var firstRowButtons = LocalComponent.Row(withdraw, edit, extend);

            if (listing.Product is MarketItem)
                firstRowButtons.AddComponent(LocalComponent.Button("buy", "Buy").WithStyle(LocalButtonComponentStyle.Success).WithIsDisabled(!listing.IsActive()));
            else
                firstRowButtons.AddComponent(LocalComponent.Button($"accept{type}", "Accept Offer").WithStyle(LocalButtonComponentStyle.Success).WithIsDisabled(listing.CurrentOffer == null));

            if (!listing.IsActive() || listing.Product is MarketItem)
                return new LocalRowComponent[] { firstRowButtons };

            var secondRowButtons = ParticipantButtons(listing);

            return new LocalRowComponent[] { firstRowButtons, secondRowButtons };
        }

        private static LocalRowComponent ParticipantButtons(Listing listing) => listing switch
        {
            { Product: AuctionItem auctionItem } => LocalComponent.Row(
                    LocalComponent.Button("undobid", "Undo Bid").WithStyle(LocalButtonComponentStyle.Danger).WithIsDisabled(listing.CurrentOffer == null),
                    LocalComponent.Button("minbid", $"Min Bid [{auctionItem.MinIncrement()}]").WithStyle(LocalButtonComponentStyle.Primary).WithIsDisabled(listing is VickreyAuction),
                    LocalComponent.Button("maxbid", $"Max Bid [{auctionItem.MaxIncrement()}]").WithStyle(LocalButtonComponentStyle.Primary).WithIsDisabled(!auctionItem.BidIncrement.MaxValue.HasValue || listing is VickreyAuction),
                    LocalComponent.Button("autobid", "Auto Bid").WithStyle(LocalButtonComponentStyle.Success).WithIsDisabled(listing is VickreyAuction)),
            _ => LocalComponent.Row(LocalComponent.Button("undo", "Undo Offer").WithStyle(LocalButtonComponentStyle.Danger).WithIsDisabled(listing.CurrentOffer == null))
        };

        private static LocalEmbed WithProductDetails(this LocalEmbed embed, Listing listing) => listing.Product switch
        {
            AuctionItem auction => embed.AddInlineField("Quantity", auction.Quantity.Amount.ToString())
                                        .AddInlineField("Starting Price", auction.StartingPrice.ToString())
                                        .AddInlineField("Current Bid", listing is VickreyAuction || auction.Offers.Count == 0
                                                                     ? listing.ValueTag.ToString()
                                                                     : $"{listing.ValueTag}\n{Mention.User(listing.CurrentOffer.UserReference.Value)}")
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledEnd))
                                        .AddInlineField("Expires In", Markdown.Timestamp(listing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime))
                                        .AddInlineField("Item Owner", listing.Anonymous
                                                                    ? Markdown.BoldItalics("Anonymous")
                                                                    : Mention.User(listing.Owner.ReferenceNumber.Value)),
            MarketItem marketItem => embed.AddInlineField("Quantity", marketItem.Quantity.Amount.ToString())
                                          .AddInlineField("Price", marketItem.CurrentPrice.ToString())
                                          .AddInlineField("Scheduled Start", 0)
                                          .AddInlineField("Scheduled Start", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledStart)),
            _ => embed
        };

        private static LocalEmbedAuthor UniqueTrait(this Listing listing) => listing switch
        {
            StandardAuction auction => auction.BuyNowPrice == null ? null : new LocalEmbedAuthor().WithName($"Instant Purchase Price: {auction.BuyNowPrice}"),
            VickreyAuction auction => auction.MaxParticipants == 0 ? null : new LocalEmbedAuthor().WithName($"Max Participants: {auction.MaxParticipants}"),
            LiveAuction auction => auction.Timeout == TimeSpan.Zero ? null : new LocalEmbedAuthor().WithName($"Bidding Timeout: {auction.Timeout.Add(TimeSpan.FromSeconds(1)).Humanize()}"),
            StandardMarket market => market.DiscountValue == 0 ? null : new LocalEmbedAuthor().WithName($"Discount: {(market.Discount == Discount.Percent ? $"{market.DiscountValue}%" : market.ValueTag.ToString())}"),
            _ => null
        };

        public static bool IsActive(this Listing listing)
        {
            return listing.Status == ListingStatus.Active
                || listing.Status == ListingStatus.Locked
                || (listing.Status == ListingStatus.Listed && listing.ScheduledPeriod.ScheduledStart.ToUniversalTime().Subtract(SystemClock.Now) <= TimeSpan.FromSeconds(5));
        }

        private static string MinIncrement(this AuctionItem auction) => auction.FormatIncrement(auction.BidIncrement.MinValue);
        private static string MaxIncrement(this AuctionItem auction) => auction.FormatIncrement(auction.BidIncrement.MaxValue.GetValueOrDefault());

        private static string FormatIncrement(this AuctionItem auction, decimal value) => value switch
        {
            0 => "Unlimited",
            >= 10000 => decimal.ToDouble(value).ToMetric(),
            _ => Money.Create(value, auction.StartingPrice.Currency).ToString(),
        };

        private static DateTimeOffset ExpiresAt(this Listing listing)
        {
            if (listing.CurrentOffer == null || listing is not LiveAuction live) return listing.ExpirationDate;

            return DateTimeOffset.UtcNow.Add(live.Timeout);
        }
    }
}
