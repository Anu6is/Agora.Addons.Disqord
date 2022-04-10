using Agora.Addons.Disqord.Extensions;
using Disqord;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class AuditChannelView : ChannelSelectionView
    {
        public AuditChannelView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) 
            : base(context, settingsOptions, new LocalMessage().AddEmbed(context.Settings.AsEmbed(settingsOptions.FirstOrDefault(s => s.IsDefault)?.Name)))
        {
            DefaultView = () => new MainSettingsView(context);
            CurrentChannelId = context.Settings.AuditLogChannelId;
        }

        public async override ValueTask SaveChannelAsync()
        {
            var settings = (DefaultDiscordGuildSettings)Context.Settings;

            using var scope = Context.Services.CreateScope();
            settings.AuditLogChannelId = SelectedChannelId;

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Send(new UpdateGuildSettingsCommand(settings));

            TemplateMessage.WithEmbeds(settings.AsEmbed("Audit Logs", new LocalEmoji("📃")));

            return;
        }
    }
}
