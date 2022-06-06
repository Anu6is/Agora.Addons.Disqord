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
            var edit = new LocalButtonComponent().WithCustomId("edit").WithLabel("Edit").WithStyle(LocalButtonComponentStyle.Primary);
            var extend = new LocalButtonComponent().WithCustomId("extend").WithLabel("Extend").WithStyle(LocalButtonComponentStyle.Primary);
            var withdraw = new LocalButtonComponent().WithCustomId($"withdraw{type}").WithLabel("Withdraw").WithStyle(LocalButtonComponentStyle.Danger);
            var firstRowButtons = new LocalRowComponent().WithComponents(new LocalButtonComponent[] { withdraw, edit, extend});

            if (listing.Product is MarketItem)
                firstRowButtons.AddComponent(new LocalButtonComponent() { CustomId = "buy", Label = "Buy", Style = LocalButtonComponentStyle.Success, IsDisabled = !listing.IsActive() });
            else
                firstRowButtons.AddComponent(new LocalButtonComponent() { CustomId = $"accept{type}", Label = "Accept Offer", Style = LocalButtonComponentStyle.Success, IsDisabled = listing.CurrentOffer == null });

            if (!listing.IsActive() || listing.Product is MarketItem)
                return new LocalRowComponent[] { firstRowButtons };
            
            var secondRowButtons = ParticipantButtons(listing);

            return new LocalRowComponent[] { firstRowButtons, secondRowButtons };
        }

        private static LocalRowComponent ParticipantButtons(Listing listing) => listing switch
        {
            { Product: AuctionItem auctionItem } => new LocalRowComponent()
            {
                Components = new LocalButtonComponent[]
                {
                    new LocalButtonComponent() { CustomId = "undobid", Label = "Undo Bid", Style = LocalButtonComponentStyle.Danger, IsDisabled = listing.CurrentOffer == null },
                    new LocalButtonComponent()
                    { 
                        CustomId = "minbid",
                        Label = $"Min Bid [{auctionItem.FormatIncrement(auctionItem.BidIncrement.MinValue)}]", 
                        Style = LocalButtonComponentStyle.Primary,
                        IsDisabled = listing is VickreyAuction                        
                    },
                    new LocalButtonComponent() 
                    { 
                        CustomId = "maxbid",
                        Label = $"Max Bid [{auctionItem.FormatIncrement(auctionItem.BidIncrement.MaxValue.GetValueOrDefault())}]",
                        Style = LocalButtonComponentStyle.Primary, 
                        IsDisabled = !auctionItem.BidIncrement.MaxValue.HasValue || listing is VickreyAuction
                    },
                    new LocalButtonComponent() 
                    { 
                        CustomId = "autobid",  
                        Label = "Auto Bid", 
                        Style = LocalButtonComponentStyle.Success,
                        IsDisabled = listing is VickreyAuction
                    },
                }
            },
            _ => new LocalRowComponent()
            {
                Components = new LocalButtonComponent[]
                {
                    new LocalButtonComponent() { CustomId = "undo",  Label = "Undo Offer", Style = LocalButtonComponentStyle.Danger, IsDisabled = listing.CurrentOffer == null },
                }
            }
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
                                        .AddInlineField("Expires In", Markdown.Timestamp(listing.ExpirationDate, Markdown.TimestampFormat.RelativeTime))
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
            LiveAuction auction => auction.Timeout == TimeSpan.Zero ? null : new LocalEmbedAuthor().WithName($"Bidding Timeout: {auction.Timeout.Humanize()}"),
            StandardMarket market => market.DiscountValue == 0 ? null : new LocalEmbedAuthor().WithName($"Discount: {(market.Discount == Discount.Percent ? $"{market.DiscountValue}%" : market.ValueTag.ToString())}"),
            _ => null
        };
        
        public static bool IsActive(this Listing listing)
        {
            return listing.Status == ListingStatus.Active
                || listing.Status == ListingStatus.Locked
                || (listing.Status == ListingStatus.Listed && listing.ScheduledPeriod.ScheduledStart.ToUniversalTime().Subtract(SystemClock.Now) <= TimeSpan.FromSeconds(5));
        }

        private static string FormatIncrement(this AuctionItem auction, decimal value) => value switch
        {
            0 => "Unlimited",
            >= 10000 => decimal.ToDouble(value).ToMetric(),
            _ => Money.Create(value, auction.StartingPrice.Currency).ToString(),
        };
    }
}
