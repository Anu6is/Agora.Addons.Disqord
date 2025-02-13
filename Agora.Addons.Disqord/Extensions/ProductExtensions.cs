﻿using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Extension;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Agora.Addons.Disqord.Extensions;

public static class ProductExtensions
{
    private const string Pattern = @"^<:\w+:\d+>$";
    private const ulong ScheduleSoldEmoteId = 397165177545424926;
    private const ulong ScheduleExpiredEmoteId = 420539480202543114;
    private const ulong ScheduleAllEmoteId = 420539509483110401;

    private static readonly string ScheduleSoldEmoteUrl = Discord.Cdn.GetCustomEmojiUrl(ScheduleSoldEmoteId);
    private static readonly string ScheduleExpiredEmoteUrl = Discord.Cdn.GetCustomEmojiUrl(ScheduleExpiredEmoteId);
    private static readonly string ScheduleAllEmoteUrl = Discord.Cdn.GetCustomEmojiUrl(ScheduleAllEmoteId);

    public static string GetScheduleEmojiUrl(this RescheduleOption rescheduleOption)
    {
        return rescheduleOption switch
        {
            RescheduleOption.Always => ScheduleAllEmoteUrl,
            RescheduleOption.Sold => ScheduleSoldEmoteUrl,
            RescheduleOption.Expired => ScheduleExpiredEmoteUrl,
            _ => string.Empty
        };
    }

    public static LocalEmbed ToEmbed(this Listing listing)
    {
        var prefix = listing.Status >= ListingStatus.Expired
            ? "Completed "
            : listing.Product is AuctionItem auctionItem && auctionItem.IsReversed
                ? "Reverse "
                : string.Empty;

        var suffix = listing is CommissionTrade ? " Request" : string.Empty;
        var type = listing is RaffleGiveaway
            ? "Raffle"
            : listing.Type.ToString();
        var iconUrl = listing.ReschedulingChoice switch
        {
            RescheduleOption.Always => ScheduleAllEmoteUrl,
            RescheduleOption.Sold => ScheduleSoldEmoteUrl,
            RescheduleOption.Expired => ScheduleExpiredEmoteUrl,
            _ => string.Empty
        };

        return new LocalEmbed
        {
            Title = $"{prefix}{type}{suffix}: {listing.Product.Title.Value}",
            Author = listing.UniqueTrait(),
            Description = listing.Product.Description?.Value,
            Url = listing.Product.Carousel?.Images.FirstOrDefault()?.Url,
            ImageUrl = listing.Product.Carousel?.Images.FirstOrDefault()?.Url,
            Footer = new LocalEmbedFooter().WithText($"Reference Code: {listing.ReferenceCode.Code()}")
                                           .WithIconUrl(iconUrl)
        }
        .WithProductDetails(listing)
        .WithRoleRestrictions(listing)
        .WithDefaultColor();
    }

    public static LocalRowComponent[] Buttons(this Listing listing, IServiceScopeFactory scopeFactory, bool earlyAcceptance, bool hideMinButton = false)
    {
        var type = listing.Type.ToString();

        if (listing.Status == ListingStatus.Sold)
        {
            var relist = LocalComponent.Button($"revert{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Revert Transaction")).WithStyle(LocalButtonComponentStyle.Danger);
            var confirm = LocalComponent.Button($"confirm{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Confirm Transaction")).WithStyle(LocalButtonComponentStyle.Success);

            return new LocalRowComponent[] { LocalComponent.Row(relist, confirm) };
        }

        var edit = LocalComponent.Button($"edit{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Edit")).WithStyle(LocalButtonComponentStyle.Primary);
        var extend = LocalComponent.Button($"extend{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Extend")).WithStyle(LocalButtonComponentStyle.Primary);
        var withdraw = LocalComponent.Button($"withdraw{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Withdraw")).WithStyle(LocalButtonComponentStyle.Danger);
        var firstRowButtons = LocalComponent.Row(withdraw, edit, extend);

        switch (listing)
        {
            case { Product: AuctionItem }:
                firstRowButtons.AddComponent(LocalComponent.Button($"accept{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Accept Offer"))
                               .WithStyle(LocalButtonComponentStyle.Success)
                               .WithIsDisabled(!earlyAcceptance || listing.CurrentOffer == null));
                break;
            case MassMarket:
                firstRowButtons.AddComponent(LocalComponent.Button("claim", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Buy [X]"))
                               .WithStyle(LocalButtonComponentStyle.Success)
                               .WithIsDisabled(!listing.IsActive()));
                break;
            case MultiItemMarket items:
                if (items.CostPerBundle > 0)
                    firstRowButtons.AddComponent(LocalComponent.Button($"bundle:{items.AmountPerBundle}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Buy Bundle"))
                                   .WithStyle(LocalButtonComponentStyle.Success)
                                   .WithIsDisabled(items.AmountPerBundle > items.Product.Quantity.Amount || !listing.IsActive()));
                break;
            case StandardTrade trade:
                if (trade.AllowOffers && listing.Product is TradeItem item)
                    firstRowButtons.AddComponent(LocalComponent.Button($"#offers", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "View Offers"))
                                                               .WithStyle(LocalButtonComponentStyle.Primary)
                                                               .WithIsDisabled(item.Offers.Count == 0));

                firstRowButtons.AddComponent(LocalComponent.Button(trade.AllowOffers ? "barter" : "trade", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, trade.AllowOffers ? "Submit Offer" : "Claim"))
                                                           .WithStyle(LocalButtonComponentStyle.Success)
                                                           .WithIsDisabled(!listing.IsActive()));
                break;
            case CommissionTrade:
                firstRowButtons.AddComponent(LocalComponent.Button("sell", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Claim"))
                               .WithStyle(LocalButtonComponentStyle.Success)
                               .WithIsDisabled(!listing.IsActive()));
                break;
            case StandardGiveaway or RaffleGiveaway:
                var giveaway = (GiveawayItem)listing.Product;
                var soldOut = giveaway.MaxParticipants > 0 && giveaway.Offers.Count == giveaway.MaxParticipants;

                firstRowButtons.AddComponent(LocalComponent.Button($"accept{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Draw Now"))
                               .WithStyle(LocalButtonComponentStyle.Success)
                               .WithIsDisabled(listing.CurrentOffer == null || (!soldOut && !earlyAcceptance)));
                break;
            default:
                break;
        }

        if (listing is StandardMarket { AllowOffers: true })
            firstRowButtons.AddComponent(LocalComponent.Button($"accept{type}", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Accept Offer"))
                           .WithStyle(LocalButtonComponentStyle.Success)
                           .WithIsDisabled(listing.CurrentOffer == null));
        else if (listing.Product is MarketItem)
            firstRowButtons.AddComponent(LocalComponent.Button(listing is MultiItemMarket ? "buy1" : "buy", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Buy"))
                           .WithStyle(LocalButtonComponentStyle.Success)
                           .WithIsDisabled(!listing.IsActive()));

        var secondRowButtons = ParticipantButtons(scopeFactory, listing, hideMinButton);

        firstRowButtons.Components = firstRowButtons.Components.Value.Where(x => (x as LocalButtonComponentBase).IsDisabled != true).ToList();

        if (secondRowButtons is not null)
            secondRowButtons.Components = secondRowButtons.Components.Value.Where(x => (x as LocalButtonComponentBase).IsDisabled != true).ToList();


        if (!listing.IsActive() || secondRowButtons == null || !secondRowButtons.Components.Value.Any())
            return new LocalRowComponent[] { firstRowButtons };
        else
            return new LocalRowComponent[] { firstRowButtons, secondRowButtons };
    }

    public static LocalEmbed WithCategory(this LocalEmbed embed, string category)
    {
        if (category.IsNull()) return embed;

        embed.AddField("Category", category);

        return embed;
    }

    private static LocalRowComponent ParticipantButtons(IServiceScopeFactory scopeFactory, Listing listing, bool hideMinButton = false) => listing switch
    {
        { Product: AuctionItem auctionItem } => auctionItem.StartingPrice.Currency.Symbol.StartsWith("<:") && Regex.IsMatch(auctionItem.StartingPrice.Currency.Symbol, Pattern)
            ? LocalComponent.Row(
                LocalComponent.Button("undobid", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Undo Bid"))
                              .WithStyle(LocalButtonComponentStyle.Danger)
                              .WithIsDisabled(listing.CurrentOffer == null),
                LocalComponent.Button("minbid", $"{TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Min Bid")} [ (+) {auctionItem.MinIncrement()}]")
                              .WithStyle(LocalButtonComponentStyle.Primary)
                              .WithIsDisabled(listing is VickreyAuction || hideMinButton)
                              .WithEmoji(LocalEmoji.FromString(auctionItem.StartingPrice.Currency.Symbol)),
                LocalComponent.Button("custombid", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Custom Bid"))
                              .WithStyle(LocalButtonComponentStyle.Success)
                              .WithEmoji(LocalEmoji.FromString(auctionItem.StartingPrice.Currency.Symbol)),
                new LocalButtonComponent().AsMaxButton(scopeFactory, listing, withEmoji: true)
                )
            : LocalComponent.Row(
                LocalComponent.Button("undobid", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Undo Bid"))
                              .WithStyle(LocalButtonComponentStyle.Danger)
                              .WithIsDisabled(listing.CurrentOffer == null),
                LocalComponent.Button("minbid", $"{TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Min Bid")} [ (+) {auctionItem.MinIncrement()}]")
                              .WithStyle(LocalButtonComponentStyle.Primary)
                              .WithIsDisabled(listing is VickreyAuction || hideMinButton),
                LocalComponent.Button("custombid", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Custom Bid"))
                              .WithStyle(LocalButtonComponentStyle.Success),
                new LocalButtonComponent().AsMaxButton(scopeFactory, listing, withEmoji: false)
                ),
        { Product: GiveawayItem giveawayItem } =>
            LocalComponent.Row(
                LocalComponent.Button("optout", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Cancel Ticket"))
                              .WithStyle(LocalButtonComponentStyle.Danger)
                              .WithIsDisabled(listing.CurrentOffer == null),
                LocalComponent.Button("join", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Get Ticket"))
                              .WithStyle(LocalButtonComponentStyle.Success)
                              .WithIsDisabled(giveawayItem.MaxParticipants > 0 && giveawayItem.MaxParticipants == giveawayItem.Offers.Count)),
        StandardMarket { AllowOffers: true } =>
            LocalComponent.Row(
                LocalComponent.Button($"revertMarket", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Undo Offer"))
                              .WithStyle(LocalButtonComponentStyle.Danger)
                              .WithIsDisabled(listing.CurrentOffer == null),
                LocalComponent.Button("bestOffer", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Make Offer"))
                              .WithStyle(LocalButtonComponentStyle.Primary),
                LocalComponent.Button("buy", TranslateButton(scopeFactory, listing.Owner.EmporiumId.Value, "Buy Now"))
                              .WithStyle(LocalButtonComponentStyle.Success)
                ),
        StandardTrade trade => trade.AllowOffers
                            ? null // TODO add buttons for bartering 
                            : null,
        _ => null
    };

    private static LocalButtonComponent AsMaxButton(this LocalButtonComponent component, IServiceScopeFactory scopeFactory, Listing listing, bool withEmoji = false)
    {
        if (listing.Product is not AuctionItem auctionItem) return null;

        var isVickrey = listing is VickreyAuction;
        var guildId = listing.Owner.EmporiumId.Value;
        var hasMaxLimit = auctionItem.BidIncrement.MaxValue.HasValue;
        var allowInstantPurchase = listing is StandardAuction { BuyNowPrice: not null } || listing is LiveAuction { BuyNowPrice: not null };

        if (!isVickrey && !hasMaxLimit && allowInstantPurchase)
            component.WithCustomId("instant").WithLabel($"{TranslateButton(scopeFactory, guildId, "Buy Now")} [{listing.BuyNowPrice()}]")
                     .WithStyle(LocalButtonComponentStyle.Success);
        else
            component.WithCustomId("maxbid").WithLabel($"{TranslateButton(scopeFactory, guildId, "Max Bid")} [ (+) {auctionItem.MaxIncrement()}]")
                     .WithStyle(LocalButtonComponentStyle.Primary).WithIsDisabled(isVickrey || !hasMaxLimit);

        if (withEmoji) component.WithEmoji(LocalEmoji.FromString(auctionItem.StartingPrice.Currency.Symbol));

        return component;
    }

    private static LocalEmbed WithProductDetails(this LocalEmbed embed, Listing listing) => listing.Product switch
    {
        GiveawayItem giveaway => embed.AddInlineField("Total Spots", giveaway.MaxParticipants == 0 ? "Unlimited" : giveaway.MaxParticipants.ToString())
                                      .AddInlineField("Joined", giveaway.Offers.Count)
                                      .AddPriceDetailField(listing)
                                      .AddInlineField("Scheduled Start", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledStart))
                                      .AddInlineField("Scheduled End", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledEnd))
                                      .AddInlineField("Expiration", Markdown.Timestamp(listing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime))
                                      .AddInlineField("Item Owner", listing.Anonymous
                                                                ? Markdown.BoldItalics("Anonymous")
                                                                : Mention.User(listing.Owner.ReferenceNumber.Value)),
        AuctionItem auction => embed.AddInlineField("Quantity", auction.Quantity.Amount.ToString())
                                    .AddInlineField("Starting Price", auction.StartingPrice.ToString())
                                    .AddInlineField("Current Bid", auction.Offers.Count == 0
                                                                 ? "No Bids"
                                                                 : listing is VickreyAuction
                                                                    ? listing.Anonymous ? "Ongoing" : $"{auction.Offers.Count}"
                                                                    : $"{listing.ValueTag}{(listing.Anonymous ? string.Empty : $"\n{Mention.User(listing.CurrentOffer.UserReference.Value)}")}")
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
        TradeItem tradeItem => embed.AddTradeOfferFields(listing)
                                    .AddInlineField("Scheduled Start", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledStart))
                                    .AddInlineField("Scheduled End", Markdown.Timestamp(listing.ScheduledPeriod.ScheduledEnd))
                                    .AddInlineField("Expiration", Markdown.Timestamp(listing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime))
                                    .AddField(listing is CommissionTrade ? "Requester" : "Item Owner", listing.Anonymous
                                                            ? Markdown.BoldItalics("Anonymous")
                                                            : Mention.User(listing.Owner.ReferenceNumber.Value)),
        _ => embed
    };

    private static LocalEmbed WithRoleRestrictions(this LocalEmbed embed, Listing listing)
    {
        if (listing.AccessRoles is null || listing.AccessRoles.Length == 0) return embed;

        embed.AddInlineField("Restricted To", string.Join(" | ", listing.AccessRoles.Select(id => Mention.Role(ulong.Parse(id)))));

        return embed;
    }

    public static List<LocalEmbed> WithImages(this Listing listing)
    {
        var embeds = new List<LocalEmbed>();
        var images = listing.Product.Carousel.Images;
        var url = images.First().Url;

        foreach (var image in images.Skip(1))
            embeds.Add(new LocalEmbed().WithUrl(url).WithImageUrl(image.Url));

        return embeds;
    }

    private static LocalEmbedAuthor UniqueTrait(this Listing listing) => listing switch
    {
        StandardAuction auction => auction.BuyNowPrice == null ? null : new LocalEmbedAuthor().WithName($"Instant Purchase Price: {auction.BuyNowPrice.Value.ToString($"N{auction.BuyNowPrice.Currency.DecimalDigits}")}"),
        VickreyAuction auction => auction.MaxParticipants == 0 ? null : new LocalEmbedAuthor().WithName($"Max Participants: {auction.MaxParticipants}"),
        LiveAuction auction => auction.Timeout == TimeSpan.Zero ? null : new LocalEmbedAuthor().WithName($"Bidding Timeout: {auction.Timeout.Add(TimeSpan.FromSeconds(1)).Humanize()} {(auction.BuyNowPrice == null ? "" : $"| Instant Purchase Price: {auction.BuyNowPrice.Value.ToString($"N{auction.BuyNowPrice.Currency.DecimalDigits}")}")}"),
        StandardMarket market => market.DiscountValue == 0 ? null : new LocalEmbedAuthor().WithName($"Discount: {market.FormatDiscount()}"),
        FlashMarket market => !market.IsActive() ? null : new LocalEmbedAuthor().WithName($"Limited Time Discount: {market.FormatDiscount()}"),
        MassMarket market => new LocalEmbedAuthor().WithName($"Cost per Item: {market.CostPerItem.Value.ToString($"N{market.CostPerItem.Currency.DecimalDigits}")}"),
        MultiItemMarket market => new LocalEmbedAuthor().WithName($"Limited to 1 per purchase {(market.AmountPerBundle > 0 ? $"or {market.AmountPerBundle} per bundle" : "")}"),
        StandardGiveaway giveaway => giveaway.Product is GiveawayItem item && item.TotalWinners > 1 ? new LocalEmbedAuthor().WithName($"Total winners: {item.TotalWinners}") : null,
        RaffleGiveaway giveaway => new LocalEmbedAuthor().WithName($"Max tickets per user: {(giveaway.MaxTicketsPerUser == 0 ? "Unlimited" : giveaway.MaxTicketsPerUser)}"),
        _ => null
    };

    public static bool IsActive(this Listing listing)
    {
        return listing.Status == ListingStatus.Active
            || listing.Status == ListingStatus.Locked
            || (listing.Status == ListingStatus.Listed && listing.ScheduledPeriod.ScheduledStart.ToUniversalTime().Subtract(SystemClock.Now) <= TimeSpan.FromSeconds(5));
    }

    private static string MinIncrement(this AuctionItem auction) => $"{(auction.IsReversed ? "-" : "")}{auction.FormatIncrement(auction.BidIncrement.MinValue)}";
    private static string MaxIncrement(this AuctionItem auction) => $"{(auction.IsReversed ? "-" : "")}{auction.FormatIncrement(auction.BidIncrement.MaxValue.GetValueOrDefault())}";
    private static string BuyNowPrice(this Listing listing) => listing is LiveAuction live
        ? live.FormatInstantPurchatePrice(live.BuyNowPrice.Value)
        : listing is StandardAuction standard
            ? standard.FormatInstantPurchatePrice(standard.BuyNowPrice.Value)
            : throw new InvalidOperationException();

    private static string FormatInstantPurchatePrice(this Listing listing, decimal value)
    {
        if (listing.Product is AuctionItem auction) return auction.FormatIncrement(value);

        return string.Empty;
    }
    private static string FormatIncrement(this AuctionItem auction, decimal value) => value switch
    {
        0 => "Unlimited",
        >= 10000 => decimal.ToDouble(value).ToMetric(),
        _ => auction.StartingPrice.Currency.Symbol.StartsWith("<:") && Regex.IsMatch(auction.StartingPrice.Currency.Symbol, Pattern)
            ? Money.Create(value, auction.StartingPrice.Currency).ToString().Replace(auction.StartingPrice.Currency.Symbol, "")
            : Money.Create(value, auction.StartingPrice.Currency).ToString(),
    };

    private static string FormatMarketPrice(this Listing listing)
    {
        var product = listing.Product as MarketItem;

        switch (listing)
        {
            case FlashMarket market:
                if (market.DiscountEndDate.ToUniversalTime() < SystemClock.Now)
                    return product.CurrentPrice.ToString();
                else
                    return $"{Markdown.Strikethrough(product.Price)}{Environment.NewLine}{Markdown.Bold(product.CurrentPrice)}";
            case MassMarket:
            case MultiItemMarket:
                return product.CurrentPrice.ToString();
            default:
                return product.Price.ToString();
        }
    }

    private static string FormatDiscount(this Listing market)
    {
        if (market.Type != ListingType.Market) return string.Empty;
        if (market is MassMarket or MultiItemMarket) return string.Empty;

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
        var percent = (discountValue / item.Price.Value) * 100;

        return percent % 1 == 0 ? $"{percent:F0}%" : $"{percent:F2}%";
    }

    private static LocalEmbed AddPriceDetailField(this LocalEmbed embed, Listing listing) => listing switch
    {
        StandardMarket market => embed.AddInlineField(market.AllowOffers ? "Best Offer" : "Discounted Price",
                                                      market.AllowOffers
            ? listing.CurrentOffer ?? Offer.Create(listing.Owner, listing.ProductId, Tag.Create("No Offers Submitted"))
            : market.DiscountValue == 0
                ? Markdown.Italics("No Discount Applied")
                : (market.Product as MarketItem).CurrentPrice.ToString()),
        FlashMarket market => embed.AddInlineField("Discount Ends", market.DiscountEndDate.ToUniversalTime() < SystemClock.Now
            ? Markdown.Italics("Expired")
            : market.IsActive() ? Markdown.Timestamp(market.DiscountEndDate, Markdown.TimestampFormat.RelativeTime) : Markdown.Bold("||To be announced...||")),
        MassMarket market => embed.AddInlineField("Bundle Bonus", market.AmountPerBundle == 0
            ? Markdown.Italics("No Bundles Defined")
            : $"{Markdown.Bold(market.AmountPerBundle)} for {Markdown.Bold(market.CostPerBundle)}"),
        MultiItemMarket market => embed.AddInlineField("Bundle Bonus", market.AmountPerBundle == 0
            ? Markdown.Italics("No Bundles Defined")
            : $"{Markdown.Bold(market.AmountPerBundle)} for {Markdown.Bold(market.CostPerBundle)}"),
        StandardGiveaway => embed.AddInlineBlankField(),
        RaffleGiveaway => embed.AddInlineField("Ticket Price", (listing.Product as GiveawayItem).TicketPrice.ToString()),
        _ => embed
    };

    private static LocalEmbed AddTradeOfferFields(this LocalEmbed embed, Listing listing)
    {
        var name = listing is CommissionTrade ? "Offering" : "Trading For";

        string value = listing is CommissionTrade request ? request.Commission.ToString() : (listing.Product as TradeItem).SuggestedOffer.Trim('"');

        embed.AddInlineField(name, value);

        return listing switch
        {
            StandardTrade { Product: TradeItem product } => product.Offers.Count != 0
                ? embed.AddInlineField("Submitted Offers", product.SuggestedOffer).AddInlineBlankField()
                : embed.AddInlineBlankField().AddInlineBlankField(),
            _ => embed.AddInlineBlankField().AddInlineBlankField()
        };
    }

    public static DateTimeOffset ExpiresAt(this Listing listing)
    {
        if (listing.Status == ListingStatus.Expired || listing.Status == ListingStatus.Sold) return DateTimeOffset.UtcNow;

        if (listing.CurrentOffer == null || listing is not LiveAuction live) return listing.ExpirationDate;

        return DateTimeOffset.UtcNow.Add(live.Timeout);
    }

    public static Money Value(this Product product) => product switch
    {
        MarketItem market => market.Price,
        AuctionItem auction => auction.CurrentPrice,
        GiveawayItem giveaway => Money.Create(giveaway.TicketPrice.Value * giveaway.Offers.Count, giveaway.TicketPrice.Currency),
        _ => null
    };

    private static string TranslateButton(IServiceScopeFactory scopeFactory, ulong guildId, string key)
    {
        using var scope = scopeFactory.CreateScope();
        var locale = scope.ServiceProvider.GetRequiredService<DiscordBot>().GetGuild(guildId).PreferredLocale;
        var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
        localization.SetCulture(locale);

        return localization.Translate(key, "ButtonStrings");
    }
}
