﻿using Emporia.Extensions.Discord;

namespace Agora.Addons.Disqord.Menus.View
{
    public class MainSettingsView : ServerSettingsView
    {
        public MainSettingsView(GuildSettingsContext context)
            : base(context,
                  new List<GuildSettingsOption>
                  {
                      new ("Server Time", "Used to determine the timezone of the server.", (ctx, opts) => new ServerTimeView(ctx, opts)),
                      new ("Server Economy", "Set if a server balance is required to execute transactions.", (ctx, opts) => new EconomyView(ctx, opts)),
                      new ("Default Currency", "Currency to use if one is not specified when listing an item.", (ctx, opts) => new DefaultCurrencyView(ctx, opts)),
                      new ("Result Logs", "Channel to send the results of completed listings to.", (ctx, opts) => new ResultChannelView(ctx, opts)),
                      new ("Audit Logs", "Channel to log all the actions performed on a listed item.", (ctx, opts) => new AuditChannelView(ctx, opts)),
                      new ("Duration Settings", "Minimum/Maximum amount of time an item can be listed.",(ctx, opts) => new ListingDurationView(ctx, opts)),
                      new ("Early Offer Acceptance", "Accept offers made and close a listing before time ends.",(ctx, opts) => new ToggleFeatureView(SettingsFlags.AcceptOffers, "Early Acceptance", ctx, opts)),
                      new ("Min Bid Button", "Enable/Disable the min bid button for auctions",(ctx, opts) => new ToggleFeatureView(SettingsFlags.HideMinMaxButtons, "Min Bid Button", ctx, opts, true)),
                      new ("Snipe Trigger", "Remaining time before a bid will trigger an extension.", (ctx, opts) => new SnipeTriggerView(ctx, opts)),
                      new ("Snipe Extension", "Duration to extend by if a bid is made within the trigger range.", (ctx, opts) => new SnipeExtensionView(ctx, opts)),
                      new ("Bidding Recall Limit", "The amount of time allowed before a bid can be recalled.", (ctx, opts) => new BiddingRecallView(ctx, opts)),
                      new ("Shill Bidding", "Allow item owners to bid on their item listings.", (ctx, opts) => new ToggleFeatureView(SettingsFlags.ShillBidding, "Shill Bidding", ctx, opts)),
                      new ("Confirm Transactions", "Confirm Market and Trade transactions before they are closed", (ctx, opts) => new ToggleFeatureView(SettingsFlags.ConfirmTransactions, "Transaction Confirmation", ctx, opts)),
                      new ("Recall Listings", "Allow users to withdraw a listing after an offer was made.", (ctx, opts) => new ToggleFeatureView(SettingsFlags.RecallListings, "Recall Listings", ctx, opts)),
                      new ("Manager Role", "Role that has admin privilages for listed items.", (ctx, opts) => new RoleAssignmentView(ctx, opts)),
                      new ("Broker Role", "Role that can create listing for other users.", (ctx, opts) => new RoleAssignmentView(ctx, opts)),
                      new ("Merchant Role", "Role that can create listings. Default: @everyone.", (ctx, opts) => new RoleAssignmentView(ctx, opts)),
                      new ("Buyer Role", "Role that can bid/claim listings. Default: @everyone.", (ctx, opts) => new RoleAssignmentView(ctx, opts)),
                      new ("Allowed Listings", "Various listing types that users can create", (ctx, opts) => new ListingsOptionsView(ctx, opts))
                    })
        { }
    }
}