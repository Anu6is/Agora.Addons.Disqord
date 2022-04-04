using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Gateway;

namespace Agora.Addons.Disqord.Menus.View
{
    internal abstract class ChannelSelectionView : BaseGuildSettingsView
    {
        public GuildSettingsContext Context { get; }        
        public ulong CurrentChannelId { get; set; }
        
        public ChannelSelectionView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
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
                    selection.Placeholder = "Select a channel category to populate options";

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
                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault) is { } defaultOption) defaultOption.IsDefault = false;
                
                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
                
                var category = ulong.Parse(e.SelectedOptions[0].Value);
                var textChannels = Context.Guild.GetChannels().Values.OfType<CachedTextChannel>();
                var textSelection = (SelectionViewComponent) EnumerateComponents().First(x => x.Row == 2);
                
                textSelection.Options.Clear();

                textChannels.Where(x => x.Id != CurrentChannelId && x.CategoryId == category).Take(25) //TODO - exclude current channel
                    .Select(channel => new LocalSelectionComponentOption(channel.Name, channel.Id.ToString())).ToList()
                    .ForEach(component => textSelection.Options.Add(component));

                if (textSelection.Options.Count > 0) 
                {
                    textSelection.Placeholder = "Select a log channel";
                    textSelection.IsDisabled = false;
                }
            }

            return default;
        }

        [Selection(MaximumSelectedOptions = 1, MinimumSelectedOptions = 1, Row = 2, Placeholder = "Select a text channel.")]
        [SelectionOption("No available text channels", Value = "0")]
        public async ValueTask SelectResultLogChannel(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0)
            {
                var selectedChannelId = ulong.Parse(e.SelectedOptions[0].Value);
                
                if (selectedChannelId == 0ul) return;
                if (selectedChannelId == CurrentChannelId) return;

                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault) is { } defaultOption) defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                await SaveChannelAsync(selectedChannelId);

                foreach (var selection in EnumerateComponents().OfType<SelectionViewComponent>())
                    if (selection.Row != null) selection.IsDisabled = true;
            }

            return;
        }

        public abstract ValueTask SaveChannelAsync(ulong selectedChannelId);
    }
}
