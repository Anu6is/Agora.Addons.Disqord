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
                                        .AddInlineField("Expiration", Markdown.Timestamp(listing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime))
                                        .AddInlineField("Item Owner", listing.Anonymous
                                                                    ? Markdown.BoldItalics("Anonymous")
                                                                    : Mention.User(listing.Owner.ReferenceNumber.Value)),
            MarketItem marketItem => embed.AddInlineField("Quantity", marketItem.Quantity.Amount.ToString())
                                          .AddInlineField("Price", listing.FormatMarketPrice())
                                          .AddPriceDetailField(listing)
                                          .AddInlineField("Scheduled Start", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledStart))
                                          .AddInlineField("Scheduled End", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledEnd))
                                          .AddInlineField("Expiration", Markdown.Timestamp(listing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime))
                                          .AddInlineField("Item Owner", listing.Anonymous
                                                                    ? Markdown.BoldItalics("Anonymous")
                                                                    : Mention.User(listing.Owner.ReferenceNumber.Value)),
            _ => embed
        };

        private static LocalEmbedAuthor UniqueTrait(this Listing listing) => listing switch
        {
            StandardAuction auction => auction.BuyNowPrice == null ? null : new LocalEmbedAuthor().WithName($"Instant Purchase Price: {auction.BuyNowPrice}"),
            VickreyAuction auction => auction.MaxParticipants == 0 ? null : new LocalEmbedAuthor().WithName($"Max Participants: {auction.MaxParticipants}"),
            LiveAuction auction => auction.Timeout == TimeSpan.Zero ? null : new LocalEmbedAuthor().WithName($"Bidding Timeout: {auction.Timeout.Add(TimeSpan.FromSeconds(1)).Humanize()}"),
            StandardMarket market => market.DiscountValue == 0 ? null : new LocalEmbedAuthor().WithName($"Discount: {market.FormatDiscount()}"),
            FlashMarket market => !market.IsActive() ? null : new LocalEmbedAuthor().WithName($"Limited Time Discount: {market.FormatDiscount()}"),
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

        private static string FormatMarketPrice(this Listing listing)
        {
            var product = listing.Product as MarketItem;

            switch (listing)
            {
                case StandardMarket: 
                    return product.Price.ToString();
                case FlashMarket market:
                    if (market.DiscountEndDate.ToUniversalTime() < SystemClock.Now)
                        return product.CurrentPrice.ToString();
                    else
                        return $"{Markdown.Strikethrough(product.Price)}{Environment.NewLine}{Markdown.Bold(product.CurrentPrice)}";
                case MassMarket:
                    if (listing.CurrentOffer == null)
                        return product.Price.ToString();
                    else
                        return Markdown.Strikethrough(product.Price.ToString());
                default:
                    return string.Empty;
            }
        }

        private static string FormatDiscount(this Listing market)
        {
            if (market.Type != ListingType.Market) return string.Empty;
            if (market is MassMarket) return string.Empty;

            Discount discount = Discount.None;
            decimal discountValue = 0;

            if (market is StandardMarket standardMarket)
            {
                discount = standardMarket.Discount;
                discountValue = standardMarket.DiscountValue;
            }
            else if (market is FlashMarket flashMarket)
            {
                discount = flashMarket.Discount;
                discountValue = flashMarket.DiscountValue;
            }

            if (discount == Discount.Percent) return $"{discountValue}%";

            var item = market.Product as MarketItem;
            var percent = 100 * (item.Price.Value - discountValue) / item.Price.Value;

            return percent % 1 == 0 ? percent.ToString("F0") : percent.ToString("F2");
        }

        private static LocalEmbed AddPriceDetailField(this LocalEmbed embed, Listing listing) => listing switch
        {
            StandardMarket market => embed.AddInlineField("Discounted Price", market.DiscountValue == 0 
                ? Markdown.Italics("No Disount Applied")
                : (market.Product as MarketItem).CurrentPrice.ToString()),
            FlashMarket market => embed.AddInlineField("Discount Ends", market.DiscountEndDate.ToUniversalTime() < SystemClock.Now 
                ? Markdown.Italics("Expired") 
                : market.IsActive() ? Markdown.Timestamp(market.DiscountEndDate, Markdown.TimestampFormat.RelativeTime) : Markdown.Bold("||To be announced...||")),
            MassMarket market => embed.AddInlineField("Total Per Item", (market.Product as MarketItem).CurrentPrice.ToString()),
            _ => null
        };

        private static DateTimeOffset ExpiresAt(this Listing listing)
        {
            if (listing.CurrentOffer == null || listing is not LiveAuction live) return listing.ExpirationDate;

            return DateTimeOffset.UtcNow.Add(live.Timeout);
        }
    }
}
