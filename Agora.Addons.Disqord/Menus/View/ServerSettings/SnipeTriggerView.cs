using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class SnipeTriggerView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;

        public SnipeTriggerView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
        }

        [Selection(MaximumSelectedOptions = 1, Row = 1, Placeholder = "Select a snipe trigger range.")]
        [SelectionOption("30 seconds", Value = "30")]
        [SelectionOption("1 minute", Value = "60")]
        [SelectionOption("2 minutes", Value = "120")]
        [SelectionOption("3 minutes", Value = "180")]
        [SelectionOption("5 minutes", Value = "300")]
        public async ValueTask SelectDuration(SelectionEventArgs e)
        {
            var trigger = TimeSpan.Zero;
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            if (e.SelectedOptions.Count > 0)
                trigger = TimeSpan.FromSeconds(int.Parse(e.SelectedOptions[0].Value));

            if (trigger == settings.SnipeRange) return;

            settings.SnipeRange = trigger;

            using (var scope = _context.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);
                
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

                TemplateMessage.WithEmbeds(settings.ToEmbed("Snipe Trigger", new LocalEmoji("⌛")));
            }

            e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
            e.Selection.IsDisabled = true;

            ReportChanges();
            
            return;
        }
    }
}
