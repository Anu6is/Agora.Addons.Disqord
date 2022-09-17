using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class ListingRecallView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly IDiscordGuildSettings _settings;

        public ListingRecallView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions)
        {
            _context = context;
            _settings = context.Settings.DeepClone();

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Position == 1)
                    button.Label = $"{(_settings.AllowListingRecall ? "Disable" : "Enable")} Listings Recall";
            }
        }

        [Button(Label = "Listings Recall", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public ValueTask RecallListings(ButtonEventArgs e)
        {
            _settings.AllowListingRecall = !_settings.AllowListingRecall;
            e.Button.Label = $"{(_settings.AllowListingRecall ? "Disable" : "Enable")} Listings Recall";

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Recall Listings"));

            ReportChanges();

            return default; ;
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Position = 3, Row = 4, Emoji = "💾")]
        public async ValueTask SaveBidingOptions(ButtonEventArgs e)
        {
            if (_settings.AllowListingRecall == _context.Settings.AllowListingRecall) return;

            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.AllowListingRecall = _settings.AllowListingRecall;

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
