using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class BiddingAllowanceView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly IDiscordGuildSettings _settings;

        public BiddingAllowanceView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions)
        {
            _context = context;
            _settings = context.Settings.DeepClone();

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Position == 1)
                    button.Label = $"{(_settings.AllowShillBidding ? "Disable" : "Enable")} Shill Bidding";

                if (button.Position == 2)
                    button.Label = $"{(_settings.AllowAbsenteeBidding ? "Disable" : "Enable")} Absentee Bidding";
            }
        }

        [Button(Label = "Shill Bidding", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public ValueTask ShillBidding(ButtonEventArgs e)
        {
            _settings.AllowShillBidding = !_settings.AllowShillBidding;
            e.Button.Label = $"{(_settings.AllowShillBidding ? "Disable" : "Enable")} Shill Bidding";

            Selection.Options.FirstOrDefault(x => x.Label == "Shill Bidding").IsDefault = true;
            Selection.Options.FirstOrDefault(x => x.Label == "Absentee Bidding").IsDefault = false;

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Shill Bidding"));

            ReportChanges();

            return default; ;
        }

        [Button(Label = "Absentee Bidding", Style = LocalButtonComponentStyle.Primary, Position = 2, Row = 4)]
        public ValueTask AbsenteeBidding(ButtonEventArgs e)
        {
            _settings.AllowAbsenteeBidding = !_settings.AllowAbsenteeBidding;
            e.Button.Label = $"{(_settings.AllowAbsenteeBidding ? "Disable" : "Enable")} Absentee Bidding";

            Selection.Options.FirstOrDefault(x => x.Label == "Shill Bidding").IsDefault = false;
            Selection.Options.FirstOrDefault(x => x.Label == "Absentee Bidding").IsDefault = true;

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Absentee Bidding"));

            ReportChanges();

            return default;
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Position = 3, Row = 4, Emoji = "💾")]
        public async ValueTask SaveBidingOptions(ButtonEventArgs e)
        {
            if (_settings.AllowShillBidding == _context.Settings.AllowShillBidding
                && _settings.AllowAbsenteeBidding == _context.Settings.AllowAbsenteeBidding) return;

            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.AllowShillBidding = _settings.AllowShillBidding;
            settings.AllowAbsenteeBidding = _settings.AllowAbsenteeBidding;

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
