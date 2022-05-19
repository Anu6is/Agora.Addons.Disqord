using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Models;
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
    public class MarketRoomView : ChannelSelectionView
    {
        private readonly List<ShowroomModel> _showrooms;

        public MarketRoomView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<ShowroomModel> showrooms)
            : base(context, settingsOptions, new LocalMessage().AddEmbed(context.Settings.ToEmbed(showrooms)))
        {
            DefaultView = () => new MainShowroomView(context, showrooms);
            _showrooms = showrooms;
        }

        [Button(Label = "Remove Room", Style = LocalButtonComponentStyle.Danger, Row = 4)]
        public async ValueTask DeleteRoom(ButtonEventArgs e)
        {
            using var scope = Context.Services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new DeleteShowroomCommand(new EmporiumId(Context.Guild.Id), new ShowroomId(SelectedChannelId), ListingType.Market));

            _showrooms.RemoveAll(x => x.ShowroomId == SelectedChannelId && x.ListingType == ListingType.Market);

            TemplateMessage.WithEmbeds(Context.Settings.ToEmbed(_showrooms));

            ReportChanges();
        }
        
        [Button(Label = "Update Hours", Style = LocalButtonComponentStyle.Primary, Row = 4)]
        public ValueTask UpdateHours(ButtonEventArgs e)
        {
            //TODO - add text input modal
            return default;
        }

        public async override ValueTask SaveChannelAsync(SelectionEventArgs e)
        {
            if (_showrooms.Any(x => x.ShowroomId == SelectedChannelId && x.ListingType == ListingType.Market)) return;

            var settings = (DefaultDiscordGuildSettings)Context.Settings;

            using var scope = Context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);
        
            var data = scope.ServiceProvider.GetRequiredService<IDataAccessor>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            await data.BeginTransactionAsync(async () =>
            {
                await mediator.Send(new CreateShowroomCommand(new EmporiumId(Context.Guild.Id), new ShowroomId(SelectedChannelId), ListingType.Market));

                if (settings.AvailableRooms.Add("Market"))
                    await mediator.Send(new UpdateGuildSettingsCommand(settings));

                _showrooms.Add(new ShowroomModel(SelectedChannelId) { ListingType = ListingType.Market, IsActive = true });
            });

            TemplateMessage.WithEmbeds(settings.ToEmbed(_showrooms));

            return;
        }

        public override ValueTask UpdateAsync()
        {
            var exists = _showrooms.Any(x => x.ShowroomId == SelectedChannelId && x.ListingType == ListingType.Market);

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
                button.IsDisabled = !exists;

            return base.UpdateAsync();
        }

        public override ValueTask LockSelectionAsync() => default;
    }
}
