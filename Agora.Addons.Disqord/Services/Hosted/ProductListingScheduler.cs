﻿using Disqord.Bot.Hosting;
using Emporia.Extensions.Discord.Services;
using Emporia.Processing.Scheduler;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class ProductListingScheduler : DiscordBotService, IJobScheduler
    {
        private readonly ILogger _logger;
        private readonly RecurringJob[] _jobs;

        public ProductListingScheduler(ListingActivationJob activationJob,
                                       ListingExpirationJob expirationJob,
                                       PendingClosureJob pendingClosureJob,
                                       DiscountExpirationJob discountExpirationJob,
                                       ILogger<ProductListingScheduler> logger)
        {
            _logger = logger;
            _jobs = new RecurringJob[] { activationJob, expirationJob, pendingClosureJob, discountExpirationJob };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Bot.WaitUntilReadyAsync(stoppingToken);

            _logger.LogTrace("Starting Product Listing Scheduler");

            foreach (var job in _jobs)
                await EnqueueAsync(job, stoppingToken);

            await base.ExecuteAsync(stoppingToken);
        }

        public async Task EnqueueAsync(IJob job, CancellationToken stoppingToken)
        {
            await (job as RecurringJob).RepeatAsync(stoppingToken);

            _logger.LogTrace("Queued Job: {RecurringJob}", job.GetType().Name);

            return;
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogTrace("Stopping Product Listing Scheduler");

            foreach (var job in _jobs)
                job.Dispose();

            return base.StopAsync(stoppingToken);
        }
    }
}
