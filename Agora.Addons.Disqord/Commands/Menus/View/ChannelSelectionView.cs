using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Gateway;
using Disqord.Rest;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public abstract class ChannelSelectionView : BaseSettingsView
    {
        protected virtual bool AllowAutoGeneration { get; }
        public GuildSettingsContext Context { get; }
        public ulong CurrentChannelId { get; set; }
        public ulong SelectedChannelId { get; private set; }

        public ChannelSelectionView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, Action<LocalMessageBase> messageTemplate)
            : base(context, settingsOptions, messageTemplate)
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
                    selection.Placeholder = "Select a channel category to populate the options.";

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
                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption) defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                var category = ulong.Parse(e.SelectedOptions[0].Value.ToString());
                var textChannels = Context.Guild.GetChannels().Values.OfType<CachedTextChannel>();
                var textSelection = (SelectionViewComponent)EnumerateComponents().First(x => x.Row == 2);

                textSelection.Options.Clear();

                if (AllowAutoGeneration)
                    textSelection.Options.Add(new LocalSelectionComponentOption("Auto-Generate showrooms", category.ToString()));

                textChannels.Where(x => x.Id != CurrentChannelId && x.CategoryId == category).Take(25)
                    .Select(channel => new LocalSelectionComponentOption(channel.Name, channel.Id.ToString())).ToList()
                    .ForEach(component => textSelection.Options.Add(component));

                if (textSelection.Options.Count > 0)
                    textSelection.Placeholder = "Select a text channel";
                else
                    textSelection.Placeholder = "No available text channels";

                textSelection.IsDisabled = false;
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

                if (!HasRequiredPermissions(out var missingPermissions))
                {
                    await e.Interaction.Response().SendMessageAsync(
                        new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(
                            new LocalEmbed()
                                .WithDescription($"The bot lacks the necessary permissions ({missingPermissions}) in the selected channel.")
                                .WithColor(Color.Red)));

                    return;
                }

                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption) defaultOption.IsDefault = false;

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

        public bool HasRequiredPermissions(out Permissions missingPermissions) 
        {
            missingPermissions = default;

            var requiredPermissions = CheckForPermissions();

            if (requiredPermissions != default)
            {
                var bot = Context.Services.GetRequiredService<AgoraBot>();
                var channel = bot.GetChannel(Context.Guild.Id, SelectedChannelId);

                if (channel == null) throw new InvalidOperationException("Unable to locate the specified channel.");

                var currentMember = bot.GetCurrentMember(Context.Guild.Id);
                var currentPermissions = currentMember.CalculateChannelPermissions(channel);

                if (!currentPermissions.HasFlag(requiredPermissions))
                    missingPermissions = requiredPermissions & ~currentPermissions;

                return missingPermissions == Permissions.None;
            }

            return true;
        }

        public virtual Permissions CheckForPermissions() => default;

        public abstract ValueTask SaveChannelAsync(SelectionEventArgs e);
    }
}
