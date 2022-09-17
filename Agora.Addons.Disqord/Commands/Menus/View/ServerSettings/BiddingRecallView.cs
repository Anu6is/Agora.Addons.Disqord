using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class BiddingRecallView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;

        public BiddingRecallView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
        }

        [Selection(MaximumSelectedOptions = 1, Row = 1, Placeholder = "Select the bidding recall limit.")]
        [SelectionOption("Disable", Value = "0")]
        [SelectionOption("5 seconds", Value = "5")]
        [SelectionOption("10 seconds", Value = "10")]
        [SelectionOption("15 seconds", Value = "15")]
        [SelectionOption("30 seconds", Value = "30")]
        public async ValueTask SelectDuration(SelectionEventArgs e)
        {
            var limit = TimeSpan.Zero;
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            if (e.SelectedOptions.Count > 0)
                limit = TimeSpan.FromSeconds(int.Parse(e.SelectedOptions[0].Value.ToString()));

            if (limit == settings.BiddingRecallLimit) return;

            settings.BiddingRecallLimit = limit;

            using (var scope = _context.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

                MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("Bidding Recall Limit", new LocalEmoji("⌛")));
            }

            e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
            e.Selection.IsDisabled = true;

            ReportChanges();

            return;
        }
    }
}
