using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class ToggleFeatureView : ServerSettingsView
    {
        private readonly string _featureText;
        private readonly SettingsFlags _flag;
        private readonly GuildSettingsContext _context;
        private readonly IDiscordGuildSettings _settings;

        public ToggleFeatureView(SettingsFlags flag, 
                                 string featureText,
                                 GuildSettingsContext context,
                                 List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions)
        {
            _flag = flag;
            _context = context;
            _featureText = featureText;
            _settings = context.Settings.DeepClone();

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Position == 1)
                    button.Label = $"{(_settings.Features.HasFlag(_flag) ? "Disable" : "Enable")} {featureText}";
            }
        }

        [Button(Label = "Toggle", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public ValueTask ConfirmTransactions(ButtonEventArgs e)
        {
            _settings.Flags = _settings.Features.ToggleFlag(_flag);
            e.Button.Label = $"{(_settings.Features.HasFlag(_flag) ? "Disable" : "Enable")} {_featureText}";

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed(_featureText));

            ReportChanges();

            return default; ;
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Position = 3, Row = 4, Emoji = "💾")]
        public async ValueTask SaveOptions(ButtonEventArgs e)
        {
            if (_settings.Flags == _context.Settings.Flags) return;

            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.Flags = _settings.Flags;

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

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }
    }
}
