using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class PersistentButtonService : DiscordBotService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        
        public PersistentButtonService(DiscordBotBase bot, IServiceScopeFactory scopeFactory, ILogger<PersistentButtonService> logger) : base(logger, bot)
        {
            _scopeFactory = scopeFactory;
        }
        
        protected override async ValueTask OnInteractionReceived(InteractionReceivedEventArgs e)
        {
            await Task.Yield();
            
            if (e.Interaction is IComponentInteraction interaction 
                && interaction.ComponentType == ComponentType.Button 
                && interaction.Message.Author.Id == Bot.CurrentUser.Id) 
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);
                
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var command = HandleInteraction(interaction);
                
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

                    await interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral(true));
                    await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);
                }

                if (!interaction.Response().HasResponded)
                    await HandleResponse(interaction);
            }

            return;
        }

        private static IBaseRequest HandleInteraction(IComponentInteraction interaction) => interaction.CustomId switch
        {
            "withdraw" => new WithdrawListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id)),
            "minbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMinimum = true },
            "maxbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMaximum = true },
            _ => null
        };

        private static Task HandleResponse(IComponentInteraction interaction) => interaction.CustomId switch
        {
            "withdraw" => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Listing successfuly withdrawn!").WithIsEphemeral(true)),
            "buy" => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Congratulations on your purchase!").WithIsEphemeral(true)),
            _ => Task.CompletedTask
        };
    }
}
