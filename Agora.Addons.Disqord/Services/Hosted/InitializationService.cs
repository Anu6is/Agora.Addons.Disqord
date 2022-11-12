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
        private readonly Random _random;
        private readonly IMessageBroker _messageBroker;
        private readonly ILogger<InitializationService> _logger;

        private readonly List<LocalActivity> _activities = new()
        {
            new LocalActivity("/server setup", ActivityType.Watching),
            new LocalActivity("/tips", ActivityType.Watching)
        };

        public InitializationService(DiscordBotBase bot, Random random, IMessageBroker messageBroker, ILogger<InitializationService> logger)
            : base(logger, bot)
        {
            _random = random;
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

            await ResetLiveAuctionsAsync();

            _logger.LogInformation("Initialized...updating status");

            while (!stoppingToken.IsCancellationRequested) 
            {
                foreach (var activity in _activities)
                {
                    await Client.SetPresenceAsync(UserStatus.Online, activity, cancellationToken: stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(_random.Next(5, 30)), stoppingToken);
                }
            }

            return;
        }

        //TODO - live auction reset
        private Task ResetLiveAuctionsAsync()
        {
            //get all active live auctions
            
            //get time since last bid 
            //if expired ? end : reset

            return Task.CompletedTask;
        }
    }
}