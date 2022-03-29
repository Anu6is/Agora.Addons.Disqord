namespace Agora.Addons.Disqord.Menus.View
{
    public class MainSettingsView : GuildSettingsView
    {
        public MainSettingsView(GuildSettingsContext context) :
            base(context,
                new List<GuildSettingsOption>
                {
                new GuildSettingsOption("Server Time", "Used to determine the timezone of the server.", (ctx, opts) => new ServerTimeView(ctx, opts)),
                new GuildSettingsOption("DO NOT SELECT", "Used to determine the timezone of the server.", (ctx, opts) => new ServerTimeView(ctx, opts))
                }) { }
    }
}
//public static IReadOnlyDictionary<string, string> Definitions
//{
//    get => new Dictionary<string, string>
//    {
//        { "Server Time", "Used to determine the timezone of the server." },
//        { "Default Currency", "Currency to use if one is not specified when listing an item." },
//        { "Snipe Trigger", "Remaining listing time before a bid will trigger an extension." },
//        { "Snipe Extension", "Duration to extend by if a bid is made within the trigger range." },
//        { "Shill Bidding", "Allow item owners to bid on their item listings." },
//        { "Absentee Bidding", "Set a bid that will be applied automatically in increments." },
//        { "Result Logs", "Channel to send the results of completed listings to." },
//        { "Audit Logs", "Channel to log all the actions performed on a listed item." },
//        { "Manager Role", "Role that has admin privilages for listed items." },
//        { "Broker Role", "Role that can create listing for other users." },
//        { "Merchant Role", "Role that can create/purchase listings. Default: @everyone." },
//        { "Allowed Listings", "Various listing types that users can create" }
//    };
//}