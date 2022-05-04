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
    public class ResultChannelView : ChannelSelectionView
    {
        public ResultChannelView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) 
            : base(context, settingsOptions, new LocalMessage().AddEmbed(context.Settings.ToEmbed(settingsOptions.FirstOrDefault(s => s.IsDefault)?.Name)))
        {
            DefaultView = () => new MainSettingsView(context);
            CurrentChannelId = context.Settings.ResultLogChannelId;
        }

        public async override ValueTask SaveChannelAsync(SelectionEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings) Context.Settings;

            using var scope = Context.Services.CreateScope();
            settings.ResultLogChannelId = SelectedChannelId;

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var emporiumId = new EmporiumId(Context.Guild.Id);
            var referenceNumber = ReferenceNumber.Create(e.AuthorId);

            scope.ServiceProvider.GetRequiredService<ICurrentUserService>().CurrentUser = EmporiumUser.Create(emporiumId, referenceNumber);
            
            await mediator.Send(new UpdateGuildSettingsCommand(settings));

            TemplateMessage.WithEmbeds(settings.ToEmbed("Result Logs", new LocalEmoji("📃")));

            return;
        }
    }
}
