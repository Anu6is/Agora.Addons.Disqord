using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Gateway;

namespace Agora.Addons.Disqord.Menus.View
{
    public abstract class ChannelSelectionView : BaseSettingsView
    {
        public GuildSettingsContext Context { get; }        
        public ulong CurrentChannelId { get; set; }
        public ulong SelectedChannelId { get; private set; }

        public ChannelSelectionView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, LocalMessage templateMessage) 
            : base(context, settingsOptions, templateMessage)
        {
            Context = context;

            var categoryChannels = context.Guild.GetChannels().Values.OfType<CachedCategoryChannel>();
            
            foreach (var selection in EnumerateComponents().OfType<SelectionViewComponent>())
            {                
                if (selection.Row == null) continue;

                selection.Options.Clear();

                if (selection.Row == 1)
                    categoryChannels.Take(25)
                        .Select(channel => new LocalSelectionComponentOption(channel.Name, channel.Id.ToString())).ToList()
                        .ForEach(component => selection.Options.Add(component));
                else if (selection.Row == 2)
                    selection.Placeholder = "Select a channel category to populate the options below";

                if (selection.Options.Count == 0)
                {
                    selection.Options.Add(new LocalSelectionComponentOption("No channel options available", "0"));
                    selection.IsDisabled = true;
                }
            }
        }

        [Selection(MaximumSelectedOptions = 1, MinimumSelectedOptions = 1, Row = 1, Placeholder = "Select a channel category.")]
        [SelectionOption("No available category channels", Value = "0")]
        public ValueTask SelectCategoryChannel(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0) 
            {
                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.Value) is { } defaultOption) defaultOption.IsDefault = false;
                
                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
                
                var category = ulong.Parse(e.SelectedOptions[0].Value.ToString());
                var textChannels = Context.Guild.GetChannels().Values.OfType<CachedTextChannel>();
                var textSelection = (SelectionViewComponent) EnumerateComponents().First(x => x.Row == 2);
                
                textSelection.Options.Clear();

                textChannels.Where(x => x.Id != CurrentChannelId && x.CategoryId == category).Take(25)
                    .Select(channel => new LocalSelectionComponentOption(channel.Name, channel.Id.ToString())).ToList()
                    .ForEach(component => textSelection.Options.Add(component));

                if (textSelection.Options.Count > 0) 
                {
                    textSelection.Placeholder = "Select a text channel";
                    textSelection.IsDisabled = false;
                }
            }

            return default;
        }

        [Selection(MaximumSelectedOptions = 1, MinimumSelectedOptions = 1, Row = 2, Placeholder = "Select a text channel.")]
        [SelectionOption("No available text channels", Value = "0")]
        public async ValueTask SelectTextChannel(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0)
            {
                SelectedChannelId = ulong.Parse(e.SelectedOptions[0].Value.ToString());
                
                if (SelectedChannelId == 0ul) return;
                if (SelectedChannelId == CurrentChannelId) return;

                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.Value) is { } defaultOption) defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                await SaveChannelAsync(e);
                await LockSelectionAsync();
                
                ReportChanges();
            }

            return;
        }

        public virtual ValueTask LockSelectionAsync()
        {
            foreach (var selection in EnumerateComponents().OfType<SelectionViewComponent>())
                if (selection.Row != null) selection.IsDisabled = true;

            return default;
        }

        public abstract ValueTask SaveChannelAsync(SelectionEventArgs e);
    }
}
