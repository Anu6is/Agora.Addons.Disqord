using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Models;
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
    public class TradeRoomView : ChannelSelectionView
    {
        private readonly List<ShowroomModel> _showrooms;

        public TradeRoomView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<ShowroomModel> showrooms)
            : base(context, settingsOptions, new LocalMessage().AddEmbed(context.Settings.ToEmbed(showrooms)))
        {
            DefaultView = () => new MainShowroomView(context, showrooms);
            _showrooms = showrooms;
        }

        public async override ValueTask SaveChannelAsync(SelectionEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)Context.Settings;

            if (settings.AvailableRooms.Add("Market"))
            {
                using var scope = Context.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);
                
                var data = scope.ServiceProvider.GetRequiredService<IDataAccessor>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await data.BeginTransactionAsync(async () =>
                {
                    //TODO - enable trade items
                    //await mediator.Send(new CreateShowroomCommand<TradeItem>(new EmporiumId(Context.Guild.Id), new ShowroomId(selectedChannelId)));
                    await mediator.Send(new UpdateGuildSettingsCommand(settings));

                    _showrooms.Add(new ShowroomModel(SelectedChannelId) { ListingType = ListingType.Market, IsActive = true });
                });
            }

            TemplateMessage.WithEmbeds(settings.ToEmbed(_showrooms));

            return;
        }
    }
}
