using Agora.Shared;
using Agora.Shared.Models;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Qmmands;
using System.Diagnostics;

namespace Agora.Addons.Disqord.Commands
{
    public sealed class BotControlModule : AgoraModuleBase
    {
        public IHost ApplicationHost { get; init; }
        private readonly ILoggingLevelSwitcher _switcher;
        private readonly IConfiguration _configuration;
        private readonly Random _random;

        public BotControlModule(Random random, IHost applicationHost, IConfiguration configuration, ILoggingLevelSwitcher switcher)
        {
            ApplicationHost = applicationHost;
            _configuration = configuration;
            _switcher = switcher;
            _random = random;
        }

        [SlashCommand("ping")]
        [Description("Test application responsiveness.")]
        [RateLimit(1, 5, RateLimitMeasure.Seconds, RateLimitBucketType.Guild)]
        public async Task<IResult> Ping() => await GetStatsAsync();

        [SlashCommand("tips")]
        [Description("Quick tips about Auction Bot")]
        public IResult Tips()
        {
            var tips = _configuration.GetSection("Facts").Get<List<Fact>>();

            return View(new QuickTipsView(tips.OrderBy(x => _random.Next()), Context.GuildLocale));
        }

        private async Task<IResult> GetStatsAsync()
        {
            if (!await Context.Bot.IsOwnerAsync(Context.AuthorId)) return Response("pong");

            var process = Process.GetCurrentProcess();
            var memory = $"{Math.Round((double)process.WorkingSet64 / (1024 * 1024))} MB | {Math.Round((double)process.PagedMemorySize64 / (1024 * 1024))} MB";
            var uptime = (DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime).Humanize(5, true, maxUnit: TimeUnit.Month, minUnit: TimeUnit.Second);

            return Response($"{memory} | {uptime}");
        }
    }
}
