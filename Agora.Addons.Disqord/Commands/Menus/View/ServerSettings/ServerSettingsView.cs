using Agora.Addons.Disqord.Extensions;
using Disqord;
using System.Drawing;

namespace Agora.Addons.Disqord.Menus.View
{
    public abstract class ServerSettingsView : BaseSettingsView
    {
        protected ServerSettingsView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) 
            : base(context, settingsOptions, message => message.AddEmbed(context.Settings.ToEmbed(settingsOptions.FirstOrDefault(s => s.IsDefault)?.Name))) 
        {
            DefaultView = () => new MainSettingsView(context);
        }
    }
}