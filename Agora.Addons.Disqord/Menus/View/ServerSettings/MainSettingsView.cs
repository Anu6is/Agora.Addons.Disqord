namespace Agora.Addons.Disqord.Menus.View
{
    public class MainSettingsView : ServerSettingsView
    {
        public MainSettingsView(GuildSettingsContext context)
            : base(context,
                  new List<GuildSettingsOption>
                  {
                      new ("Server Time", "Used to determine the timezone of the server.", (ctx, opts) => new ServerTimeView(ctx, opts)),
                      new ("Default Currency", "Currency to use if one is not specified when listing an item.", (ctx, opts) => new DefaultCurrencyView(ctx, opts)),
                      new ("Result Logs", "Channel to send the results of completed listings to.", (ctx, opts) => new ResultChannelView(ctx, opts)),
                      new ("Audit Logs", "Channel to log all the actions performed on a listed item.", (ctx, opts) => new AuditChannelView(ctx, opts)),
                      new ("Snipe Trigger", "Remaining listing time before a bid will trigger an extension.", (ctx, opts) => new SnipeTriggerView(ctx, opts)),
                      new ("Snipe Extension", "Duration to extend by if a bid is made within the trigger range.", (ctx, opts) => new SnipeExtensionView(ctx, opts)),
                      new ("Shill Bidding", "Allow item owners to bid on their item listings.", (ctx, opts) => new BiddingAllowanceView(ctx, opts)),
                      new ("Absentee Bidding", "Set a bid that will be applied automatically in increments.", (ctx, opts) => new BiddingAllowanceView(ctx, opts)),

                      new ("Manager Role", "Role that has admin privilages for listed items.", (ctx, opts) => new ServerTimeView(ctx, opts)),
                      new ("Broker Role", "Role that can create listing for other users.", (ctx, opts) => new ServerTimeView(ctx, opts)),
                      new ("Merchant Role", "Role that can create/purchase listings. Default: @everyone.", (ctx, opts) => new ServerTimeView(ctx, opts)),

                      new ("Allowed Listings", "Various listing types that users can create", (ctx, opts) => new ListingsOptionsView(ctx, opts))
                    }) { }
    }
}