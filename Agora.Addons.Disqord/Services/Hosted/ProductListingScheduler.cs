using Disqord.Bot.Hosting;
using Emporia.Processing.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class ProductListingScheduler : DiscordBotService, IJobScheduler
    {
        private readonly ILogger _logger;
        private readonly IList<RecurringJob> _jobs;

        public ProductListingScheduler(IServiceProvider provider, ILogger<ProductListingScheduler> logger)
        {
            _logger = logger;
            _jobs = provider.GetServices<RecurringJob>().ToList();
        }

        public override async Task StartAsync(CancellationToken stoppingToken)
        {
            await Bot.WaitUntilReadyAsync(stoppingToken);
            
            _logger.LogInformation("Starting Product Listing Scheduler");

            foreach (var job in _jobs)
                await EnqueueAsync(job, stoppingToken);

            await base.StartAsync(stoppingToken);
        }

        public async Task EnqueueAsync(IJob job, CancellationToken stoppingToken)
        {
            await (job as RecurringJob).RepeatAsync(stoppingToken);

            _logger.LogInformation("Queued Job: {RecurringJob}", job.GetType().Name);

            return;
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping Product Listing Scheduler");

            foreach (var job in _jobs)
                job.Dispose();

            _jobs.Clear();

            return base.StopAsync(stoppingToken);
        }
    }
}
