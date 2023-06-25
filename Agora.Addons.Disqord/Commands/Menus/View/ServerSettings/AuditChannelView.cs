using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Gateway;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class AuditChannelView : ChannelSelectionView
    {
        public AuditChannelView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions)
            : base(context, settingsOptions, message => message.AddEmbed(context.Settings.ToEmbed(settingsOptions.FirstOrDefault(s => s.IsDefault)?.Name)))
        {
            DefaultView = () => new MainSettingsView(context);
            CurrentChannelId = context.Settings.AuditLogChannelId;
        }

        public override Permissions CheckForPermissions() => Permissions.SendMessages | Permissions.SendEmbeds;

        [Button(Label = "Remove Channel", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public async ValueTask ClearAuditLogChannel(ButtonEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)Context.Settings;
            settings.AuditLogChannelId = 0;

            await SaveAsync(settings, e);
            await LockSelectionAsync();

            foreach (ButtonViewComponent button in EnumerateComponents().OfType<ButtonViewComponent>())
                if (button.Position == 1) button.IsDisabled = true;

            ReportChanges();
        }

        public async override ValueTask SaveChannelAsync(SelectionEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)Context.Settings;

            settings.AuditLogChannelId = SelectedChannelId;

            await SaveAsync(settings, e);
        }

        public async ValueTask SaveAsync(DefaultDiscordGuildSettings settings, InteractionReceivedEventArgs e)
        {
            using var scope = Context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

            MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("Audit Logs", new LocalEmoji("📃")));

            return;
        }
    }
}
