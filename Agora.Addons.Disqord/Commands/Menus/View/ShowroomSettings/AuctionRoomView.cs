﻿using Agora.Addons.Disqord.Extensions;
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
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class AuctionRoomView : ChannelSelectionView
    {
        protected override bool IncludeForumChannels => true;
        protected override bool IncludeNewsChannels => true;
        protected override bool AllowAutoGeneration => true;

        private readonly List<Showroom> _showrooms;

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
        public async ValueTask UpdateHours(ButtonEventArgs e)
        {
            var room = _showrooms.FirstOrDefault(x => x.Id.Value == SelectedChannelId && x.ListingType == ListingType.Auction.ToString());

            if (room is null)
            {
                e.Button.IsDisabled = true;
                return;
            }

            var response = new LocalInteractionModalResponse()
                .WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle($"Update Business Hours")
                .WithComponents(
                    LocalComponent.Row(new LocalTextInputComponent()
                    {
                        Style = TextInputComponentStyle.Short,
                        CustomId = "opening",
                        Label = TranslateButton("Opens At"),
                        Placeholder = room.ActiveHours?.OpensAt.ToString(@"hh\:mm") ?? "00:00",
                        MaximumInputLength = 5,
                        MinimumInputLength = 4,
                        IsRequired = true
                    }),
                    LocalComponent.Row(new LocalTextInputComponent()
                    {
                        Style = TextInputComponentStyle.Short,
                        CustomId = "closing",
                        Label = TranslateButton("Closes At"),
                        Placeholder = room.ActiveHours?.ClosesAt.ToString(@"hh\:mm") ?? "00:00",
                        MaximumInputLength = 5,
                        MinimumInputLength = 4,
                        IsRequired = true
                    }));

            await e.Interaction.Response().SendModalAsync(response);

            var reply = await Menu.Interactivity
                .WaitForInteractionAsync(e.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10), Menu.StoppingToken);

            if (reply == null) return;

            var modal = reply.Interaction as IModalSubmitInteraction;
            var rows = modal.Components.OfType<IRowComponent>();
            var opening = rows.First().Components.OfType<ITextInputComponent>().First().Value;
            var closing = rows.Last().Components.OfType<ITextInputComponent>().First().Value;

            using var scope = Context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                var opensAt = Time.From(opening);
                var closesAt = Time.From(closing);

                room.SetBusinessHours(opensAt, closesAt);

                await mediator.Send(new UpdateBusinessHoursCommand(new EmporiumId(Context.Guild.Id), room.Id, ListingType.Auction, room.ActiveHours));
            }
            catch (Exception ex)
            {
                var message = ex switch
                {
                    ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                    ArgumentOutOfRangeException => ex.Message,
                    ArgumentNullException => ex.Message,
                    FormatException => ex.Message,
                    _ => "An error occured while processing this action. If this persists, please contact support."
                };

                if (message.Contains("contact support"))
                    await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());

                return;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Business Hours Successfully Updated!"));

            MessageTemplate = message => message.WithEmbeds(Context.Settings.ToEmbed(_showrooms));

            ReportChanges();

            return;
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
                var transactionResult = await data.BeginTransactionAsync(async () =>
                {
                    var roomResult = await mediator.Send(new CreateShowroomCommand(new EmporiumId(Context.Guild.Id), new ShowroomId(SelectedChannelId), ListingType.Auction));

                    if (!roomResult.IsSuccessful) return roomResult;

                    if (settings.AvailableRooms.Add(ListingType.Auction.ToString()))
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

        public override ValueTask UpdateAsync()
        {
            var exists = _showrooms.Any(x => x.Id.Value == SelectedChannelId && x.ListingType == ListingType.Auction.ToString());

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
                if (button.Label != TranslateButton("Close"))
                    button.IsDisabled = !exists;
                else
                    button.IsDisabled = false;

            return base.UpdateAsync();
        }

        public override ValueTask LockSelectionAsync() => default;
    }
}
