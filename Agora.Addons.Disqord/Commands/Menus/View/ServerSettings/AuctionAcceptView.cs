using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agora.Addons.Disqord.Menus.View
{
    public class AuctionAcceptView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly IDiscordGuildSettings _settings;

        public AuctionAcceptView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions)
        {
            _context = context;
            _settings = context.Settings.DeepClone();

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Position == 1)
                    button.Label = $"{(_settings.AllowAcceptingOffer ? "Disable" : "Enable")} Bid Acceptance";
            }
        }

        [Button(Label = "Bid Acceptance", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public ValueTask RecallListings(ButtonEventArgs e)
        {
            _settings.AllowAcceptingOffer = !_settings.AllowAcceptingOffer;
            e.Button.Label = $"{(_settings.AllowAcceptingOffer ? "Disable" : "Enable")} Bid Acceptance";

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Allow Bid Acceptance"));

            ReportChanges();

            return default; ;
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Position = 3, Row = 4, Emoji = "💾")]
        public async ValueTask SaveBidingOptions(ButtonEventArgs e)
        {
            if (_settings.AllowAcceptingOffer == _context.Settings.AllowAcceptingOffer) return;

            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.AllowAcceptingOffer = _settings.AllowAcceptingOffer;

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
