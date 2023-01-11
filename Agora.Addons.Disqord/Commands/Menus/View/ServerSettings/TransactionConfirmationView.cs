using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class TransactionConfirmationView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly IDiscordGuildSettings _settings;

        public TransactionConfirmationView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions)
        {
            _context = context;
            _settings = context.Settings.DeepClone();

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Position == 1)
                    button.Label = $"{(_settings.TransactionConfirmation ? "Disable" : "Enable")} Transaction Confirmation";
            }
        }

        [Button(Label = "Transaction Confirmation", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public ValueTask ConfirmTransactions(ButtonEventArgs e)
        {
            _settings.TransactionConfirmation = !_settings.TransactionConfirmation;
            e.Button.Label = $"{(_settings.TransactionConfirmation ? "Disable" : "Enable")} Transaction Confirmation";

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Confirm Transactions"));

            ReportChanges();

            return default; ;
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Position = 3, Row = 4, Emoji = "💾")]
        public async ValueTask SaveOptions(ButtonEventArgs e)
        {
            if (_settings.TransactionConfirmation == _context.Settings.TransactionConfirmation) return;

            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.TransactionConfirmation = _settings.TransactionConfirmation;

            using var scope = _context.Services.CreateScope();
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

                MessageTemplate = message => message.WithEmbeds(settings.ToEmbed());
            }

            foreach (ButtonViewComponent button in EnumerateComponents().OfType<ButtonViewComponent>())
                if (button.Label != "Close") button.IsDisabled = true;

            ReportChanges();

            return;
        }
    }
}
