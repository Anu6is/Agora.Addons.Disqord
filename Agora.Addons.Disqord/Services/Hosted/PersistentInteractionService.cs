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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmporiaCacheService _cache;

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
                //TODO - validate interaction author

                var modalInteraction = await SendModalInteractionResponseAsync(interaction);

                await _cache.GetUserAsync(e.GuildId.Value, e.AuthorId);

                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var command = modalInteraction == null ? HandleInteraction(interaction) : await HandleModalInteraction(modalInteraction);

                try
                {
                    if (command != null) await mediator.Send(command);

                    if (modalInteraction != null) 
                        await HandleResponse(modalInteraction);
                    else
                        await HandleResponse(interaction);
                }
                catch (Exception ex)
                {
                    await SendErrorResponseAsync(e, modalInteraction as IInteraction ?? interaction, scope, ex);
                }
            }

            return;
        }

        private static async ValueTask SendErrorResponseAsync(InteractionReceivedEventArgs e, IInteraction interaction, IServiceScope scope, Exception ex)
        {
            var message = ex switch
            {
                ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                UnauthorizedAccessException unauthorizedAccessException => unauthorizedAccessException.Message,
                _ => "An error occured while processing this action."
            };
            
            await interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral(true));
            await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

            return;
        }

        private async Task<IModalSubmitInteraction> SendModalInteractionResponseAsync(IComponentInteraction interaction)
        {
            if (_modalRedirect.ContainsKey(interaction.CustomId))
            {
                var timeout = TimeSpan.FromMinutes(10);
                var response = _modalRedirect[interaction.CustomId].Invoke(interaction);

                await interaction.Response().SendModalAsync(response);
                
                var reply = await Client.WaitForInteractionAsync(interaction.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, timeout, Client.StoppingToken);

                return reply?.Interaction as IModalSubmitInteraction;
            }

            return null;
        }
    }
}
