using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Interactivity;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Extensions.Discord;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public partial class PersistentInteractionService : DiscordBotService
    {
        private readonly ILogger<PersistentInteractionService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmporiaCacheService _cache;

        public PersistentInteractionService(DiscordBotBase bot,
                                            IServiceScopeFactory scopeFactory,
                                            IEmporiaCacheService cache,
                                            ILogger<PersistentInteractionService> logger) : base(logger, bot)
        {
            _cache = cache;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async ValueTask OnInteractionReceived(InteractionReceivedEventArgs args)
        {
            if (args.GuildId == null) return;

            if (args.Interaction is IComponentInteraction interaction
                && interaction.ComponentType == ComponentType.Button
                && interaction.Message.Author.Id == Bot.CurrentUser.Id)
            {
                _logger.LogInformation("{Author} selected {button} in {guild}",
                                 interaction.Author.Name,
                                 interaction.CustomId,
                                 interaction.GuildId);

                if (interaction.CustomId.StartsWith("#")) return;

                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(args);
                
                var roomId = await DetermineShowroomAsync(args);

                if (roomId == 0) return;
                if (!_modalRedirect.ContainsKey(interaction.CustomId) && !_confirmationRequired.ContainsKey(interaction.CustomId)) 
                    await interaction.Response().DeferAsync();

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await AuthorizeInteractionAsync(args, interaction, roomId, scope, mediator);

                if (!await ConfirmInteractionAsync(interaction)) return;

                var modalInteraction = await SendModalInteractionResponseAsync(interaction);

                var command = modalInteraction == null
                    ? HandleInteraction(interaction, roomId)
                    : await HandleModalInteraction(modalInteraction, roomId);


                await HandleInteractionResponseAsync(args, interaction, scope, modalInteraction, command, mediator);
            }

            return;
        }

        private static async Task HandleInteractionResponseAsync(InteractionReceivedEventArgs args,
                                                                 IComponentInteraction interaction,
                                                                 IServiceScope scope,
                                                                 IModalSubmitInteraction modalInteraction,
                                                                 IBaseRequest command,
                                                                 IMediator mediator)
        {
            try
            {
                if (command != null) await mediator.Send(command);

                if (modalInteraction != null) await HandleResponse(modalInteraction);
            }
            catch (Exception ex)
            {
                await SendErrorResponseAsync(args, modalInteraction as IInteraction ?? interaction, scope, ex);
                throw;
            }
        }

        private static async Task AuthorizeInteractionAsync(InteractionReceivedEventArgs args,
                                                            IComponentInteraction interaction,
                                                            ulong roomId,
                                                            IServiceScope scope,
                                                            IMediator mediator)
        {
            var request = AuthorizeInteraction(interaction, roomId);

            if (request == null) return;

            try
            {
                await mediator.Send(request);
            }
            catch (Exception ex)
            {
                await SendErrorResponseAsync(args, interaction, scope, ex);
                throw;
            }
        }

        private async Task<bool> ConfirmInteractionAsync(IComponentInteraction interaction)
        {
            if (!_confirmationRequired.TryGetValue(interaction.CustomId, out var label)) return true;

            var components = interaction.Message.Components
                .Select(row =>
                {
                    return LocalComponent.Row(row.Components.Select(component =>
                    {
                        var button = (IButtonComponent)component;

                        return LocalComponent.Button(button.CustomId, button.Label)
                                             .WithStyle((LocalButtonComponentStyle)button.Style)
                                             .WithIsDisabled(button.IsDisabled);
                    }).ToArray());
                }).ToArray();

            await interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse()
            {
                Components = new[]
                {
                    LocalComponent.Row(
                        LocalComponent.Button($"{interaction.Message.Id}:deny", "Cancel").WithStyle(LocalButtonComponentStyle.Danger),
                        LocalComponent.Button($"{interaction.Message.Id}:confirm", label).WithStyle(LocalButtonComponentStyle.Primary))
                }
            });

            var response = await Client
                .WaitForInteractionAsync<IComponentInteraction>(interaction.ChannelId,
                                                                button => button.CustomId.StartsWith(interaction.Message.Id.ToString()) 
                                                                       && button.AuthorId == interaction.AuthorId,
                                                                TimeSpan.FromSeconds(3),
                                                                Client.StoppingToken);

            var confirmed = response != null && response.CustomId.Contains("confirm");

            await interaction.Followup().ModifyResponseAsync(x => x.Components = components);

            return confirmed;
        }

        private async Task<ulong> DetermineShowroomAsync(InteractionReceivedEventArgs e)
        {
            ulong roomId;
            var emporium = await _cache.GetEmporiumAsync(e.GuildId.Value);

            if (emporium == null) return 0ul;

            if (emporium.Showrooms.Any(x => x.Id.Value.Equals(e.ChannelId.RawValue)))
            {
                roomId = e.ChannelId.RawValue;
            }
            else
            {
                roomId = Client.GetChannel(e.GuildId.Value, e.ChannelId) switch
                {
                    ITextChannel textChannel => textChannel.CategoryId.GetValueOrDefault(),
                    IThreadChannel threadChannel => threadChannel.ChannelId,
                    _ => 0
                };
            }

            return roomId;
        }

        private static async ValueTask SendErrorResponseAsync(InteractionReceivedEventArgs e, IInteraction interaction, IServiceScope scope, Exception ex)
        {
            var message = ex switch
            {
                TimeoutException => null,
                ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                UnauthorizedAccessException unauthorizedAccessException => unauthorizedAccessException.Message,
                { } when ex.Message.Contains("interaction has already been", StringComparison.OrdinalIgnoreCase) => null,
                _ => "An error occured while processing this action. If this persists, please contact support."
            };
            
            await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

            if (message == null) return;

            var response = new LocalInteractionMessageResponse().WithIsEphemeral().WithContent(message);

            if (message.EndsWith("contact support.")) 
                response.WithComponents(LocalComponent.Row(LocalComponent.LinkButton("https://discord.gg/WmCpC8G", "Support Server")));

            if (interaction.Response().HasResponded)
                await interaction.Followup().SendAsync(new LocalInteractionFollowup().WithIsEphemeral().WithContent(message));
            else
                await interaction.Response().SendMessageAsync(response);

            return;
        }

        private async Task<IModalSubmitInteraction> SendModalInteractionResponseAsync(IComponentInteraction interaction)
        {
            if (_modalRedirect.ContainsKey(interaction.CustomId))
            {
                var timeout = TimeSpan.FromMinutes(14);
                var response = _modalRedirect[interaction.CustomId].Invoke(interaction);

                await interaction.Response().SendModalAsync(response);

                var reply = await Client.WaitForInteractionAsync(interaction.ChannelId,
                                                                 x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId,
                                                                 timeout,
                                                                 Client.StoppingToken);

                return reply?.Interaction as IModalSubmitInteraction;
            }

            return null;
        }
    }
}
