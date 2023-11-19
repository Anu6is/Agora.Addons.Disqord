using Disqord;
using Emporia.Application.Models;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using Humanizer;

namespace Agora.Addons.Disqord.Extensions
{
    public static class AuctionTemplateExtensions
    {
        public static LocalEmbed CreateEmbed(this ITemplate template) 
        {
            return template switch
            {
                AuctionTemplate auction => auction.CreateEmbed(),
                _ => new LocalEmbed(),
            };
        }

        public static LocalEmbed CreateEmbed(this AuctionTemplate template)
        {
            var field = template.Type switch
            {
                "Standard" => new LocalEmbedField().WithName("Buy Now Price").WithValue(Money.Create((decimal)template.BuyNowPrice, template.Currency)).WithIsInline(),
                "Sealed" => new LocalEmbedField().WithName("Max Participants").WithValue(template.MaxParticipants == 0 ? "Unlimited" : template.MaxParticipants).WithIsInline(),
                "Live" => new LocalEmbedField().WithName("Timeout").WithValue(template.Timeout.Humanize(precision: 2, minUnit: Humanizer.Localisation.TimeUnit.Second)).WithIsInline(),
                _ => null
            };

            var reverse = template.ReverseBidding ? "[reverse]" : string.Empty;

            return new LocalEmbed()
                .WithAuthor($"{template.Type} Auction {reverse}| {template.Name}")
                .WithTitle($"Title: {template.Title ?? ""}")
                .WithDescription($"{Markdown.Bold("Description:")}{Environment.NewLine}{template.Description ?? Markdown.CodeBlock(" ")}")
                .AddInlineField("Quantity", template.Quantity == 0 ? 1 : template.Quantity)
                .AddInlineField("Starting Price", Money.Create((decimal)template.StartingPrice, template.Currency))
                .AddInlineField("Reserved Price", Money.Create((decimal)template.ReservePrice, template.Currency))
                .AddInlineField("Duration", template.Duration.Humanize(precision: 2, minUnit: Humanizer.Localisation.TimeUnit.Second))
                .AddInlineField("Reschedule", template.Reschedule.ToString())
                .AddInlineField(field)
                .AddInlineField("Min Bid Increase", template.MinBidIncrease)
                .AddInlineField("Max Bid Increase", template.MaxBidIncrease)
                .AddInlineBlankField()
                .AddInlineField("Category", template.Category ?? Markdown.CodeBlock(" "))
                .AddInlineField("Subcategory", template.Subcategory ?? Markdown.CodeBlock(" "))
                .AddInlineBlankField()
                .AddField($"Owner {(template.Anonymous ? "[hidden]" : "[visible]")}", template.Owner == 0 ? Markdown.CodeBlock(" ") : Mention.User(template.Owner))
                .WithImageUrl(template.Image)
                .WithFooter(template.Message)
                .WithDefaultColor();
        }

        public static AuctionItemModel MapToItemModel(this AuctionTemplate template)
        {
            return new AuctionItemModel(ProductTitle.Create(template.Title), template.Currency.Code, (decimal)template.StartingPrice, Stock.Create(template.Quantity))
            {
                ImageUrl = template.Image.IsNotNull() ? new[] { template.Image } : null,
                Category = template.Category.IsNotNull() ? CategoryTitle.Create(template.Category) : null,
                Subcategory = template.Subcategory.IsNotNull() ? SubcategoryTitle.Create(template.Subcategory) : null,
                Description = template.Description.IsNotNull() ? ProductDescription.Create(template.Description) : null,
                MinBidIncrease = (decimal)template.MinBidIncrease,
                MaxBidIncrease = (decimal)template.MaxBidIncrease,
                ReservePrice = (decimal)template.ReservePrice,
                Reversed = template.ReverseBidding
            };
        }

        public static ListingModel MapToListingModel(this AuctionTemplate template, DateTime scheduledStart, DateTime scheduledEnd, UserId userId)
        {
            var message = template.Message is not null ? HiddenMessage.Create(template.Message) : null;

            ListingModel listing = template.Type switch
            {
                "Standard" => new StandardAuctionModel(scheduledStart, scheduledEnd, userId) 
                { 
                    BuyNowPrice = (decimal)template.BuyNowPrice, 
                    Anonymous = template.Anonymous,
                    HiddenMessage = message
                },
                "Sealed" => new VickreyAuctionModel(scheduledStart, scheduledEnd, userId) 
                { 
                    MaxParticipants = (uint)template.MaxParticipants,
                    Anonymous = template.Anonymous,
                    HiddenMessage = message
                },
                "Live" => new LiveAuctionModel(scheduledStart, scheduledEnd, template.Timeout, userId) 
                { 
                    BuyNowPrice = (decimal)template.BuyNowPrice,
                    Timeout = template.Timeout,
                    Anonymous = template.Anonymous,
                    HiddenMessage = message
                },
                _ => null
            };

            listing.RescheduleOption = template.Reschedule;

            return listing;
        }
    }
}
