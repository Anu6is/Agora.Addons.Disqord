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
    public class SnipeExtensionView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;

        public SnipeExtensionView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
        }

        [Selection(MaximumSelectedOptions = 1, Row = 1, Placeholder = "Select a snipe extension duration.")]
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
                duration = TimeSpan.FromSeconds(int.Parse(e.SelectedOptions[0].Value));

            if (duration == settings.SnipeExtension) return;

            using (var scope = _context.Services.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var emporiumId = new EmporiumId(_context.Guild.Id);
                var referenceNumber = ReferenceNumber.Create(e.AuthorId);

                scope.ServiceProvider.GetRequiredService<ICurrentUserService>().CurrentUser = EmporiumUser.Create(emporiumId, referenceNumber);
                settings.SnipeExtension = duration;

                await mediator.Send(new UpdateGuildSettingsCommand(settings));

                TemplateMessage.WithEmbeds(settings.ToEmbed("Snipe Extension", new LocalEmoji("⏳")));
                
                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
            }

            e.Selection.IsDisabled = true;

            ReportChanges();
            
            return;
        }
    }
}
