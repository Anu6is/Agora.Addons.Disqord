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
    public class AuctionRoomView : ChannelSelectionView
    {
        private List<Showroom> _showrooms;
        
        public AuctionRoomView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<Showroom> showrooms)
            : base(context, settingsOptions, message => message.AddEmbed(context.Settings.ToEmbed(showrooms)))
        {
            DefaultView = () => new MainShowroomView(context, showrooms);
            _showrooms = showrooms;
        }

        [Button(Label = "Remove Room", Style = LocalButtonComponentStyle.Danger, Row = 4)]
        public async ValueTask DeleteRoom(ButtonEventArgs e)
        {
            using var scope = Context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Send(new DeleteShowroomCommand(new EmporiumId(Context.Guild.Id), new ShowroomId(SelectedChannelId), ListingType.Auction));

            _showrooms.RemoveAll(x => x.Id.Value == SelectedChannelId && x.ListingType == ListingType.Auction.ToString());
            
            MessageTemplate = message => message.WithEmbeds(Context.Settings.ToEmbed(_showrooms));
            
            ReportChanges();

            return;
        }

        [Button(Label = "Update Hours", Style = LocalButtonComponentStyle.Primary, Row = 4)]
        public ValueTask UpdateHours(ButtonEventArgs e)
        {
            //TODO - add text input modal
            return default;
        }

        public async override ValueTask SaveChannelAsync(SelectionEventArgs e)
        {
            if (_showrooms.Any(x => x.Id.Value == SelectedChannelId && x.ListingType == ListingType.Auction.ToString())) return;
            
            var settings = (DefaultDiscordGuildSettings)Context.Settings;

            using var scope = Context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var data = scope.ServiceProvider.GetRequiredService<IDataAccessor>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var room = _showrooms.FirstOrDefault(x => x.Id.Value.Equals(SelectedChannelId) && x.ListingType.Equals(ListingType.Auction.ToString()));

            if (room == null)
            {
                await data.BeginTransactionAsync(async () =>
                {
                    var showroom = await mediator.Send(new CreateShowroomCommand(new EmporiumId(Context.Guild.Id), new ShowroomId(SelectedChannelId), ListingType.Auction));

                    if (settings.AvailableRooms.Add(ListingType.Auction.ToString()))
                        await mediator.Send(new UpdateGuildSettingsCommand(settings));

                    _showrooms.Add(showroom);
                });
            }
            
            MessageTemplate = message => message.WithEmbeds(settings.ToEmbed(_showrooms));

            ReportChanges();

            return;
        }
        
        public override ValueTask UpdateAsync()
        {
            var exists = _showrooms.Any(x => x.Id.Value == SelectedChannelId && x.ListingType == ListingType.Auction.ToString());

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
                if (button.Label != "Close")
                    button.IsDisabled = !exists;

            return base.UpdateAsync();
        }

        public override ValueTask LockSelectionAsync() => default;
    }
}
