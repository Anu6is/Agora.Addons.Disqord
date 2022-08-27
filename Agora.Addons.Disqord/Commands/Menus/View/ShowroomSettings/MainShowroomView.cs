using Emporia.Domain.Entities;

namespace Agora.Addons.Disqord.Menus.View
{
    public class MainShowroomView : ShowroomSettingsView
    {
        public MainShowroomView(GuildSettingsContext context, List<Showroom> showrooms)
            : base(context,
                  new List<GuildSettingsOption>
                  {
                      new ("Auction Room", "Owner lists an item which is sold to the highest bidder.", (ctx, opts) => new AuctionRoomView(ctx, opts, showrooms)),
                      new ("Market Room", "Owner lists an item which is sold to the first buyer(s).", (ctx, opts) => new MarketRoomView(ctx, opts, showrooms)),
                      new ("Trade Room", "Owner lists an item to trade for something specific or best offer.", (ctx, opts) => new TradeRoomView(ctx, opts, showrooms)),
                      //new ("Exchange Room", "User lists an item they want, and accepts the best deal to acquire it.", (ctx, opts) => new ExchangeRoomView(ctx, opts, showrooms))
                    },
                  showrooms)
        { }
    }
}
