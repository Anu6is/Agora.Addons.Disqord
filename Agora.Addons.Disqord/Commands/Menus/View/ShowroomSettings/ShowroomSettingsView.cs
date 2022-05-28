using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Models;
using Disqord;

namespace Agora.Addons.Disqord.Menus.View
{
    public abstract class ShowroomSettingsView : BaseSettingsView
    {
        public ShowroomSettingsView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<ShowroomModel> showrooms) 
            : base(context, settingsOptions, new LocalMessage().AddEmbed(context.Settings.ToEmbed(showrooms))) 
        {
            DefaultView = () => new MainShowroomView(context, showrooms);
        }
    }
}
