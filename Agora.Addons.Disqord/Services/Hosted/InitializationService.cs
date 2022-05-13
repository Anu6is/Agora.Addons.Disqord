using Disqord.Bot;
using Disqord.Bot.Hosting;
using Emporia.Application.Features.Queries;
using Emporia.Extensions.Discord.Features.MessageBroker;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class InitializationService : DiscordBotService
    {
        private readonly IMessageBroker _messageBroker;

        public InitializationService(DiscordBotBase bot, IMessageBroker messageBroker, ILogger<InitializationService> logger) : base(logger, bot)
        {
            _messageBroker = messageBroker;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = Bot.Services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var emporiumList = await mediator.Send(new GetEmporiumListQuery(), cancellationToken);

            foreach (var emporiumId in emporiumList.Data)
                await _messageBroker.TryRegisterAsync(emporiumId);

            return;
        }
    }
}
