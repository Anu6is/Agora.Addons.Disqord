using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Menus;
using Disqord;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Common;
using Emporia.Application.Specifications;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireBuyer]
    public sealed class UserUtilityModule : AgoraModuleBase
    {
        [SlashCommand("Watchlist")]
        [Description("View all active auctions on which you've placed a bid")]
        public async Task<IResult> GetUserWatchlist()
        {
            await Deferral(true);

            var userReference = ReferenceNumber.Create(Context.AuthorId);

            var listings = await Data.Transaction<IReadRepository<Listing>>()
                .ListAsync(new EntitySpec<Listing>(x => EF.Property<string>(x, "ListingType").Equals("Auction") 
                                                     && (x.Product as AuctionItem).Offers.Any(b => b.UserReference.Equals(userReference)),
                                                     includes: new[]{ "Product", "Owner" }));


            listings = listings.Where(x => x.Owner.EmporiumId.Value.Equals(Context.GuildId)).ToList();

            if (!listings.Any()) 
                return Response(new LocalEmbed().WithDefaultColor().WithDescription("There are no active listings that you've bid on"));

            return View(new WatchlistView(userReference, listings.OrderBy(x => x.ExpiresAt()).ToArray()));
        }
    }
}
