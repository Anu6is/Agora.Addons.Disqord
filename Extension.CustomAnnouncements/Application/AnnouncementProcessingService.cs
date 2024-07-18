using Agora.Shared.Attributes;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Extension.CustomAnnouncements.Domain;
using Microsoft.Extensions.Logging;

namespace Extension.CustomAnnouncements.Application;

[AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
public class AnnouncementProcessingService(DiscordBotBase bot, CustomAnnouncementService customAnnouncementService, ILogger<AnnouncementProcessingService> logger) : AgoraService(logger)
{
    public async Task<string?> GetAnnouncementMessageAsync(Listing listing)
    {
        var isForumPost = bot.GetChannel(listing.Owner.EmporiumId.Value, listing.ShowroomId.Value) is CachedForumChannel;

        var placeholders = new Dictionary<string, string>
        {
            { "owner", Mention.User(listing.Owner.ReferenceNumber.Value) },
            { "winner",  GetWinners(listing)},
            { "quantity",  GetQuantity(listing)},
            { "itemName", listing.Product.Title.Value },
            { "listingType", listing is CommissionTrade ? "Trade Request" : listing.ToString()! },
            { "forumPost", isForumPost ? Discord.MessageJumpLink(listing.Owner.EmporiumId.Value, listing.Product.ReferenceNumber.Value, listing.ReferenceCode.Reference()) : string.Empty }
        };

        var result = await customAnnouncementService.GetAnnouncementAsync(listing.Owner.EmporiumId.Value, GetAnnouncementType(listing.Type));

        if (!result.IsSuccessful)
            result = await customAnnouncementService.GetAnnouncementAsync(listing.Owner.EmporiumId.Value, AnnouncementType.Default);

        if (result.IsSuccessful) 
            return MessageExtensions.ReplacePlaceholders(result.Data, placeholders);

        return null;
    }

    private static AnnouncementType GetAnnouncementType(ListingType listingType)
    {
        string sourceName = listingType.ToString();

        if (Enum.TryParse(sourceName, out AnnouncementType target))
            return target;

        return AnnouncementType.Default;
    }

    private static string GetWinners(Listing listing)
    {
        var buyer = listing.CurrentOffer.UserReference.Value;
        var winners = listing switch
        {
            StandardGiveaway giveaway => giveaway.Winners,
            RaffleGiveaway giveaway => giveaway.Winners,
            _ => []
        };

        return winners.Length <= 1 ? Mention.User(buyer) : string.Join(" | ", winners.Select(x => Mention.User(x.UserReference.Value)));
    }

    private static string GetQuantity(Listing listing)
    {
        var stock = listing is MassMarket or MultiItemMarket
            ? (listing.Product as MarketItem)!.Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
            : listing.Product.Quantity.Amount;

        var quantity = stock == 1 ? string.Empty : $"{stock} ";
        
        return quantity;
    }
}
