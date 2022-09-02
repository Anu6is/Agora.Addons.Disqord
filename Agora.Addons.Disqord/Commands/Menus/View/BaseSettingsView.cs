using Disqord;
using Disqord.Extensions.Interactivity.Menus;

namespace Agora.Addons.Disqord.Menus
{
    public abstract class BaseSettingsView : ViewBase
    {
        private readonly GuildSettingsContext _context;
        private readonly List<GuildSettingsOption> _settingsOptions;

        public SelectionViewComponent Selection { get; }
        public Func<ViewBase> DefaultView { get; init; }

        public BaseSettingsView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, Action<LocalMessageBase> messageTemplate)
            : base(messageTemplate)
        {
            _context = context;
            _settingsOptions = settingsOptions;

            Selection = new SelectionViewComponent(HandleSelection)
            {
                MinimumSelectedOptions = 0,
                MaximumSelectedOptions = 1,
                Placeholder = "Select an option to modify"
            };

            for (var i = 0; i < settingsOptions.Count; i++)
            {
                var selectionOption = new LocalSelectionComponentOption(settingsOptions[i].Name, i.ToString())
                    .WithDescription(settingsOptions[i].Description)
                    .WithIsDefault(settingsOptions[i].IsDefault);

                Selection.Options.Add(selectionOption);
            }

            if (Selection.Options.Count > 0) AddComponent(Selection);
        }

        private ValueTask HandleSelection(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0)
            {
                if (!int.TryParse(e.SelectedOptions[0].Value.ToString(), out var value))
                    throw new InvalidOperationException("All the values of the selection's options must be page indexes");

                if (Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption)
                {
                    defaultOption.IsDefault = false;
                    _settingsOptions[int.Parse(defaultOption.Value.ToString())].IsDefault = false;
                }

                Selection.Options.FirstOrDefault(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
                _settingsOptions[value].IsDefault = true;

                Menu.View = _settingsOptions[value].GetView(_context, _settingsOptions);
            }
            else
            {
                Menu.View = DefaultView();
            }

            return default;
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Position = 4, Row = 4)]
        public ValueTask CloseView(ButtonEventArgs e) => default;

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent && buttonComponent.Label == "Close") return "Close";

            return base.GetCustomId(component); 
        }
    }
}
