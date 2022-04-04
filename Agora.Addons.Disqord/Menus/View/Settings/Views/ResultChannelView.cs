using Agora.Addons.Disqord.Extensions;
using Disqord;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    internal class ResultChannelView : ChannelSelectionView
    {
        public ResultChannelView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            CurrentChannelId = context.Settings.ResultLogChannelId;
        }

        public async override ValueTask SaveChannelAsync(ulong selectedChannelId)
        {
            var settings = (DefaultDiscordGuildSettings) Context.Settings;

            using (var scope = Context.Services.CreateScope())
            {
                settings.ResultLogChannelId = selectedChannelId;

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new UpdateGuildSettingsCommand(settings));

                TemplateMessage.WithEmbeds(settings.AsEmbed("Result Logs", new LocalEmoji("📃")));
            }

            return;
        }
    }
}
