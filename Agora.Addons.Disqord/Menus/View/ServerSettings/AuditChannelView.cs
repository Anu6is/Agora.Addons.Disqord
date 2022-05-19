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
    public class AuditChannelView : ChannelSelectionView
    {
        public AuditChannelView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) 
            : base(context, settingsOptions, new LocalMessage().AddEmbed(context.Settings.ToEmbed(settingsOptions.FirstOrDefault(s => s.IsDefault)?.Name)))
        {
            DefaultView = () => new MainSettingsView(context);
            CurrentChannelId = context.Settings.AuditLogChannelId;
        }

        public async override ValueTask SaveChannelAsync(SelectionEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)Context.Settings;
            
            settings.AuditLogChannelId = SelectedChannelId;

            using var scope = Context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

            TemplateMessage.WithEmbeds(settings.ToEmbed("Audit Logs", new LocalEmoji("📃")));

            return;
        }
    }
}
