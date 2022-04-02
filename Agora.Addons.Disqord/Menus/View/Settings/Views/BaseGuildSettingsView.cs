using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Menus.View;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;

namespace Agora.Addons.Disqord.Menus
{
    public abstract class BaseGuildSettingsView : ViewBase
    {
        private readonly GuildSettingsContext _context;
        private readonly List<GuildSettingsOption> _settingsOptions;
        
        public SelectionViewComponent Selection { get; }
        
        public BaseGuildSettingsView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions)
            : base(new LocalMessage().AddEmbed(context.Settings.AsEmbed(settingsOptions.FirstOrDefault(s => s.IsDefault)?.Name)))
        {
            _context = context;
            _settingsOptions = settingsOptions;

            Selection = new SelectionViewComponent(HandleSelection) { MinimumSelectedOptions = 0, MaximumSelectedOptions = 1 };
            
            for (var i = 0; i < settingsOptions.Count; i++)
            {
                var selectionOption = new LocalSelectionComponentOption(settingsOptions[i].Name, i.ToString())
                    .WithDescription(settingsOptions[i].Description)
                    .WithIsDefault(settingsOptions[i].IsDefault);

                Selection.Options.Add(selectionOption);
            }

            AddComponent(Selection);
        }

        private ValueTask HandleSelection(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0)
            {
                if (!int.TryParse(e.SelectedOptions[0].Value, out var value))
                    throw new InvalidOperationException("All the values of the selection's options must be page indexes");

                if (Selection.Options.FirstOrDefault(x => x.IsDefault) is { } defaultOption) 
                { 
                    defaultOption.IsDefault = false; 
                    _settingsOptions[int.Parse(defaultOption.Value)].IsDefault = false;

                }

                Selection.Options.FirstOrDefault(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
                _settingsOptions[value].IsDefault = true;

                Menu.View = _settingsOptions[value].GetView(_context, _settingsOptions);
            }
            else
            {
                Menu.View = new MainSettingsView(_context);
            }

            return default;
        }
    }
}
