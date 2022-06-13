using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
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
        private readonly List<Showroom> _showrooms;

        public TradeRoomView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<Showroom> showrooms)
            : base(context, settingsOptions, message => message.AddEmbed(context.Settings.ToEmbed(showrooms)))
        {
            DefaultView = () => new MainShowroomView(context, showrooms);
            _showrooms = showrooms;
        }

        public async override ValueTask SaveChannelAsync(SelectionEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)Context.Settings;

            using var scope = Context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var data = scope.ServiceProvider.GetRequiredService<IDataAccessor>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var cache = scope.ServiceProvider.GetRequiredService<IEmporiaCacheService>();

            var emporium = await cache.GetEmporiumAsync(Context.Guild.Id);
            var room = emporium.Showrooms.FirstOrDefault(x => x.Id.Value.Equals(SelectedChannelId) && x.ListingType.Equals(ListingType.Trade.ToString()));

            if (room == null)
            {
                await data.BeginTransactionAsync(async () =>
                {
                    var showroom = await mediator.Send(new CreateShowroomCommand(new EmporiumId(Context.Guild.Id), new ShowroomId(SelectedChannelId), ListingType.Trade));

                    if (settings.AvailableRooms.Add(ListingType.Trade.ToString()))
                        await mediator.Send(new UpdateGuildSettingsCommand(settings));

                    _showrooms.Add(showroom);
                });
            }

            MessageTemplate = message => message.WithEmbeds(settings.ToEmbed(_showrooms));

            return;
        }
    }
}
