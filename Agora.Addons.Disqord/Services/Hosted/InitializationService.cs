using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
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
        private readonly ILogger<InitializationService> _logger;

        public InitializationService(DiscordBotBase bot, IMessageBroker messageBroker, ILogger<InitializationService> logger)
            : base(logger, bot)
        {
            _logger = logger;
            _messageBroker = messageBroker;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = Bot.Services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var emporiumList = await mediator.Send(new GetEmporiumListQuery(), stoppingToken);

            foreach (var emporiumId in emporiumList.Data)
                await _messageBroker.TryRegisterAsync(emporiumId);

            await Bot.WaitUntilReadyAsync(stoppingToken);

            _logger.LogInformation("Initialized...updating status");

            await Client.SetPresenceAsync(UserStatus.Online, new LocalActivity("/Server Setup", ActivityType.Watching), cancellationToken: stoppingToken);

            await base.ExecuteAsync(stoppingToken);

            return;
        }
    }
}