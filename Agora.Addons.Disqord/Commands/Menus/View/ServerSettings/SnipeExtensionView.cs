using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class SnipeExtensionView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;

        public SnipeExtensionView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
        }

        [Selection(MaximumSelectedOptions = 1, Row = 1, Placeholder = "Select a snipe extension duration.")]
        [SelectionOption("Disable", Value = "0")]
        [SelectionOption("30 seconds", Value = "30")]
        [SelectionOption("1 minute", Value = "60")]
        [SelectionOption("5 minutes", Value = "300")]
        [SelectionOption("10 minutes", Value = "600")]
        [SelectionOption("15 minutes", Value = "900")]
        [SelectionOption("30 minutes", Value = "1800")]
        public async ValueTask SelectDuration(SelectionEventArgs e)
        {
            var duration = TimeSpan.Zero;
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            if (e.SelectedOptions.Count > 0)
                duration = TimeSpan.FromSeconds(int.Parse(e.SelectedOptions[0].Value.ToString()));

            if (duration == settings.SnipeExtension) return;

            settings.SnipeExtension = duration;

            using (var scope = _context.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

                MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("Snipe Extension", new LocalEmoji("⏳")));
            }

            e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
            e.Selection.IsDisabled = true;

            ReportChanges();

            return;
        }
    }
}
