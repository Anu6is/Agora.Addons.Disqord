using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Interactivity;
using Disqord.Gateway;
using Disqord.Models;
using Disqord.Rest;
using Disqord.Serialization.Json.Default;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using FluentValidation;
using HumanTimeParser.Core.Parsing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class PersistentInteractionService : DiscordBotService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmporiaCacheService _cache;

        private readonly Dictionary<string, Func<IComponentInteraction, LocalInteractionModalResponse>> _modalRedirect = new()
        {
            { "extendAuction", ExtendListingModal },
            { "editAuction", EditListingModal }
        };


        public PersistentInteractionService(DiscordBotBase bot, IServiceScopeFactory scopeFactory, IEmporiaCacheService cache, ILogger<PersistentInteractionService> logger) : base(logger, bot)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        protected override async ValueTask OnInteractionReceived(InteractionReceivedEventArgs e)
        {
            if (e.Interaction is IComponentInteraction interaction
                && interaction.ComponentType == ComponentType.Button
                && interaction.Message.Author.Id == Bot.CurrentUser.Id)
            {
                IModalSubmitInteraction modalInteraction = null;

                if (_modalRedirect.ContainsKey(interaction.CustomId))
                {
                    var response = _modalRedirect[interaction.CustomId].Invoke(interaction);

                    await interaction.Response().SendModalAsync(response);

                    var reply = await Client.GetInteractivity()
                        .WaitForInteractionAsync(e.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10), Client.StoppingToken);

                    if (reply == null) return;

                    modalInteraction = reply.Interaction as IModalSubmitInteraction;
                }

                await _cache.GetUserAsync(e.GuildId.Value, e.AuthorId);

                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var command = modalInteraction == null ? HandleInteraction(interaction) : await HandleModalInteraction(modalInteraction);

                try
                {
                    if (command != null)
                        await mediator.Send(command);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing interaction");

                    var message = ex switch
                    {
                        ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                        UnauthorizedAccessException unauthorizedAccessException => unauthorizedAccessException.Message,
                        _ => "An error occured while processing this action."
                    };

                    if (modalInteraction == null)
                        await interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral(true));
                    else
                        await modalInteraction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral(true));

                    await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                    return;
                }

                if (!interaction.Response().HasResponded)
                    await HandleResponse(interaction);
                else
                    await HandleResponse(modalInteraction);
            }

            return;
        }

        private static IBaseRequest HandleInteraction(IComponentInteraction interaction) => interaction.CustomId switch
        {
            "undobid" => new UndoBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id)),
            "minbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMinimum = true },
            "maxbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMaximum = true },
            { } when interaction.CustomId.StartsWith("accept") => new AcceptListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("accept", "")),
            { } when interaction.CustomId.StartsWith("withdraw") => new WithdrawListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("withdraw", "")),
            _ => null
        };

        private async Task<IBaseRequest> HandleModalInteraction(IModalSubmitInteraction modalInteraction)
        {
            var emporiumId = new EmporiumId(modalInteraction.GuildId.Value);
            var showroomId = new ShowroomId(modalInteraction.ChannelId);
            var keys = modalInteraction.CustomId.Split(':');

            switch (keys[0])
            {
                case string x when x.StartsWith("extend"):
                    return await ExtendListing(modalInteraction, emporiumId, showroomId, keys);
                case "edit":
                    break;
                default:
                    break;
            }
            throw new NotImplementedException();
        }

        private async Task<IBaseRequest> ExtendListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components.OfType<IRowComponent>();
            var selection = rows.First().Components.OfType<ISelectionComponent>().First() as ITransientEntity<ComponentJsonModel>;
            var option = selection.Model["values"].ToType<DefaultJsonArray>()[0].ToString();
            var text = rows.Last().Components.OfType<ITextInputComponent>().First().Value;

            var emporium = await Client.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(emporiumId.Value);
            var settings = await Client.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(emporiumId.Value);
            var result = Client.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(text);

            if (result is not ISuccessfulTimeParsingResult<DateTime> successfulResult) throw new ValidationException("Invalid extension format.");

            if (option == "duration")
            {
                var extension = successfulResult.Value - emporium.LocalTime.DateTime;

                return new ExtendListingCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), keys[0].Replace("extend", ""))
                {
                    Limit = settings.MaximumDuration,
                    Duration = extension
                };
            }
            else
            {
                return new ExtendListingCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), keys[0].Replace("extend", ""))
                {
                    Limit = settings.MaximumDuration,
                    ExpirationDate = successfulResult.Value
                };
            }
        }

        private static Task HandleResponse(IComponentInteraction interaction) => interaction.CustomId switch
        {
            "withdrawAuction" => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Auction listing withdrawn!").WithIsEphemeral(true)),
            "buy" => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Congratulations on your purchase!").WithIsEphemeral(true)),
            _ => Task.CompletedTask
        };

        private static Task HandleResponse(IModalSubmitInteraction interaction) => interaction.CustomId switch
        {
            { } when interaction.CustomId.StartsWith("extend") => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Product listing extended!").WithIsEphemeral(true)),
            _ => Task.CompletedTask
        };

        private static LocalInteractionModalResponse ExtendListingModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle($"Extend Expiration")
                .WithComponents(
                    LocalComponent.Row(
                        LocalComponent.Selection("option")
                            .WithMinimumSelectedOptions(1)
                            .WithMaximumSelectedOptions(1)
                            .WithPlaceholder("Select an extension option")
                            .WithOptions(
                                new LocalSelectionComponentOption("Extend By", "duration").WithDescription("Extend the product listing by a specified duration."),
                                new LocalSelectionComponentOption("Extend To", "datetime").WithDescription("Extend the product listing to a specified date and time."))
                            ),
                    LocalComponent.Row(
                        LocalComponent.TextInput("extension", "Input Extension", TextInputComponentStyle.Short)
                            .WithMinimumInputLength(2)
                            .WithMaximumInputLength(16)
                            .WithIsRequired()));
        }

        private static LocalInteractionModalResponse EditListingModal(IComponentInteraction interaction)
        {
            throw new NotImplementedException();
        }
    }
}
