using Agora.Addons.Disqord.Extensions;
using Disqord;
using Emporia.Domain.Entities;

namespace Agora.Addons.Disqord.Menus.View
{
    public abstract class ShowroomSettingsView : BaseSettingsView
    {
        public ShowroomSettingsView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<Showroom> showrooms) 
            : base(context, settingsOptions, message => message.AddEmbed(context.Settings.ToEmbed(showrooms))) 
        {
            DefaultView = () => new MainShowroomView(context, showrooms);
        }
    }
}
