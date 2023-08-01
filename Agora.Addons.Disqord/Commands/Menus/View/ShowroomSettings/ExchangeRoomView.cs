using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class ExchangeRoomView : ChannelSelectionView
    {
        protected override bool IncludeForumChannels => true;
        protected override bool IncludeNewsChannels => true;
        protected override bool AllowAutoGeneration => true;

        private readonly List<Showroom> _showrooms;

        public ExchangeRoomView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions, List<Showroom> showrooms)
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

            var room = _showrooms.FirstOrDefault(x => x.Id.Value.Equals(SelectedChannelId) && x.ListingType.Equals(ListingType.Exchange.ToString()));

            if (room == null)
            {
                var transactionResult = await data.BeginTransactionAsync(async () =>
                {
                    var roomResult = await mediator.Send(new CreateShowroomCommand(new EmporiumId(Context.Guild.Id), new ShowroomId(SelectedChannelId), ListingType.Exchange));

                    if (!roomResult.IsSuccessful) return roomResult;

                    if (settings.AvailableRooms.Add(ListingType.Exchange.ToString()))
                    {
                        var updateResult = await mediator.Send(new UpdateGuildSettingsCommand(settings));

                        if (!updateResult.IsSuccessful) return updateResult;
                    }

                    _showrooms.Add(roomResult.Data);

                    return Result.Success();
                });

                if (!transactionResult.IsSuccessful)
                {
                    await e.Interaction.Response()
                                       .SendMessageAsync(
                                            new LocalInteractionMessageResponse()
                                                .WithIsEphemeral()
                                                .AddEmbed(new LocalEmbed().WithDescription(transactionResult.FailureReason).WithDefaultColor()));
                    return;
                }
            }

            MessageTemplate = message => message.WithEmbeds(settings.ToEmbed(_showrooms));

            ReportChanges();

            return;
        }
    }
}
