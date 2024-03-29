﻿using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;

namespace Agora.Addons.Disqord.Menus.View
{
    public class MainShowroomView : ShowroomSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly List<Showroom> _showrooms;

        public MainShowroomView(GuildSettingsContext context, List<Showroom> showrooms)
            : base(context,
                  new List<GuildSettingsOption>
                  {
                      new ("Auction Room", "Owner lists an item which is sold to the highest bidder.", (ctx, opts) => new AuctionRoomView(ctx, opts, showrooms)),
                      new ("Market Room", "Owner lists an item which is sold to the first buyer(s).", (ctx, opts) => new MarketRoomView(ctx, opts, showrooms)),
                      new ("Trade Room", "Owner lists an item to trade for something specific or best offer.", (ctx, opts) => new TradeRoomView(ctx, opts, showrooms)),
                      new ("Giveaway Room", "Owner lists an item to be given to a randomly selected user.", (ctx, opts) => new GiveawayRoomView(ctx, opts, showrooms)),
                      //new ("Exchange Room", "User lists an item they want, and accepts the best deal to acquire it.", (ctx, opts) => new ExchangeRoomView(ctx, opts, showrooms))
                    },
                  showrooms)
        {
            _context = context;
            _showrooms = showrooms;
        }

        [Button(Label = "Permissions", Style = LocalButtonComponentStyle.Primary, Row = 4)]
        public async ValueTask ViewPermissions(ButtonEventArgs e)
        {
            await e.Interaction.Response().DeferAsync();

            var guildId = _context.Guild.Id;
            var bot = e.Interaction.Client as DiscordBotBase;
            var resultLogId = _context.Settings.ResultLogChannelId;
            var auditLogId = _context.Settings.AuditLogChannelId;
            var resultLog = resultLogId == 0 ? "Not Configured" : resultLogId == 1 ? "Inline Results" : bot.ValidateChannelPermissions(guildId, resultLogId, true);
            var auditLog = auditLogId == 0 ? "Not Configured" : bot.ValidateChannelPermissions(guildId, auditLogId, true);

            var auction = _showrooms.Where(x => x.ListingType == ListingType.Auction.ToString())?
                                            .Select(x => bot.ValidateChannelPermissions(guildId, x.Id.Value));
            var market = _showrooms.Where(x => x.ListingType == ListingType.Market.ToString())?
                                            .Select(x => bot.ValidateChannelPermissions(guildId, x.Id.Value));
            var trade = _showrooms.Where(x => x.ListingType == ListingType.Trade.ToString())?
                                            .Select(x => bot.ValidateChannelPermissions(guildId, x.Id.Value));

            var giveaway = _showrooms.Where(x => x.ListingType == ListingType.Giveaway.ToString())?
                                            .Select(x => bot.ValidateChannelPermissions(guildId, x.Id.Value));

            var embed = new LocalEmbed().WithTitle("Permissions Review").WithUrl("https://support.discord.com/hc/en-us/articles/206141927")
                                        .WithDescription("Channel permissions override server permissions. " +
                                        "If these checks reveal that permissions are missing, review the permissions granted to the Bot/@everyone on the channel.")
                                        .AddField("Logs", $"Result Log: {resultLog}{Environment.NewLine}Audit Log: {auditLog}")
                                        .AddField("Auction Channels", auction.Count() != 0 ? string.Join(Environment.NewLine, auction) : "None Configured")
                                        .AddField("Market Channels", market.Count() != 0 ? string.Join(Environment.NewLine, market) : "None Configured")
                                        .AddField("Trade Channels", trade.Count() != 0 ? string.Join(Environment.NewLine, trade) : "None Configured")
                                        .AddField("Giveaway Channels", giveaway.Count() != 0 ? string.Join(Environment.NewLine, giveaway) : "None Configured")
                                        .WithDefaultColor();

            await e.Interaction.Followup().SendAsync(new LocalInteractionFollowup().AddEmbed(embed).WithIsEphemeral(true));
        }

        [Button(Label = "View Settings", Style = LocalButtonComponentStyle.Success, Row = 4)]
        public ValueTask ViewSettings(ButtonEventArgs e)
        {
            Menu.View = new MainSettingsView(_context);

            return default;
        }
    }
}
