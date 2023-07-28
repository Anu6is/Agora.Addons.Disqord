using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Emporia.Application.Common;
using Emporia.Application.Features.Queries;
using Emporia.Domain.Entities;
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

            await ResetLiveAuctionsAsync(stoppingToken);

            _logger.LogDebug("Initialized...updating status");

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var activity in _activities)
                {
                    await Client.SetPresenceAsync(UserStatus.Online, activity, cancellationToken: stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(_random.Next(5, 15)), stoppingToken);
                }
            }

            return;
        }

        private async Task ResetLiveAuctionsAsync(CancellationToken cancellationToken)
        {
            using var scope = Bot.Services.CreateScope();
            var dataAccessor = scope.ServiceProvider.GetRequiredService<IDataAccessor>();

            var auctions = await dataAccessor.Transaction<IReadRepository<LiveAuction>>().ListAsync(cancellationToken);

            foreach (var auction in auctions)
            {
                if (auction.CurrentOffer is not { } offer) continue;

                var expiration = offer.SubmittedOn.Add(auction.Timeout).Subtract(TimeSpan.FromSeconds(1));
                var countdown = expiration > DateTimeOffset.UtcNow
                    ? expiration.Subtract(DateTimeOffset.UtcNow)
                    : TimeSpan.FromSeconds(_random.Next(5, 10));

                try
                {
                    _logger.LogDebug("Reset timout for live auction {auction} to {countdown}", auction.Id, countdown);

                    using var timerScope = Bot.Services.CreateScope();
                    var timer = timerScope.ServiceProvider.GetRequiredService<IAuctionTimer>();

                    await timer.Reset(auction, countdown);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to reset Live Auction {auction} ", auction.Id);
                }
            }

            return;
        }
    }
}