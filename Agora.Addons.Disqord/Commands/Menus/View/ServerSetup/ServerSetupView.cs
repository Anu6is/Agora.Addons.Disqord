using Agora.Shared.Models;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;

namespace Agora.Addons.Disqord.Menus.View
{
    public class ServerSetupView : ListingsOptionsView
    {
        private readonly GuildSettingsContext _context;

        public ServerSetupView(GuildSettingsContext context, List<GuildSettingsOption> options) : base(context, options)
        {
            _context = context;
        }

        [Button(Label = "Continue", Style = LocalButtonComponentStyle.Success, Row = 2)]
        public ValueTask Continue(ButtonEventArgs e)
        {
            Menu.View = new MainShowroomView(_context, new List<ShowroomModel>());

            return default;
        }

        public override ValueTask UpdateAsync()
        {
            ButtonViewComponent save = null;
            ButtonViewComponent @continue = null;

            foreach (ButtonViewComponent button in EnumerateComponents().OfType<ButtonViewComponent>())
                if (button.Label == "Save")
                    save = button;
                else if (button.Label == "Continue")
                    @continue = button;

            @continue.IsDisabled = !save.IsDisabled;

            return base.UpdateAsync();
        }
    }
}
