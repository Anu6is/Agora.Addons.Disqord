using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Models;
using Disqord;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class MarketRoomView : ChannelSelectionView
    {
        private readonly List<ShowroomModel> _showrooms;

        public MarketRoomView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<ShowroomModel> showrooms)
            : base(context, settingsOptions, new LocalMessage().AddEmbed(context.Settings.AsEmbed(showrooms)))
        {
            DefaultView = () => new MainShowroomView(context, showrooms);
            _showrooms = showrooms;
        }

        public async override ValueTask SaveChannelAsync()
        {
            var settings = (DefaultDiscordGuildSettings)Context.Settings;

            if (settings.AvailableRooms.Add("Market"))
            {
                using var scope = Context.Services.CreateScope();
                var data = scope.ServiceProvider.GetRequiredService<IDataAccessor>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await data.BeginTransactionAsync(async () =>
                {
                    //TODO - enable market items
                    //await mediator.Send(new CreateShowroomCommand<MarketItem>(new EmporiumId(Context.Guild.Id), new ShowroomId(selectedChannelId)));
                    await mediator.Send(new UpdateGuildSettingsCommand(settings));

                    _showrooms.Add(new ShowroomModel(SelectedChannelId) { ItemType = "MarketItem", IsActive = true });
                });
            }

            TemplateMessage.WithEmbeds(settings.AsEmbed(_showrooms));

            return;
        }
    }
}
