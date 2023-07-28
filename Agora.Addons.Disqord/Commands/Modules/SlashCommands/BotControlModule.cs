using Agora.Shared;
using Agora.Shared.Extensions;
using Agora.Shared.Models;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qmmands;
using Serilog.Events;
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

            return View(new QuickTipsView(tips.OrderBy(x => _random.Next())));
        }

        [RequireBotOwner]
        [SlashCommand("log")]
        [RequireGuild(551567205461131305)]
        [Description("Set the log level")]
        public async Task ChangeLogLevel([Description("Serilog log level")] LogEventLevel logLevel, bool includeEF = false)
        {
            _switcher.SetMinimumLevel(logLevel);

            if (includeEF)
                _switcher.SetOverrideLevel(logLevel);
            else
                _switcher.SetOverrideLevel(_configuration.GetOverrideLoglevel("Microsoft.EntityFrameworkCore"));

            await Response($"Logging set to {logLevel}");
        }

        [RequireBotOwner]
        [SlashCommand("shard-status")]
        [RequireGuild(551567205461131305)]
        [Description("Get the status of all shards")]
        public IResult Shards()
        {
            var shards = Bot.ApiClient.Shards.Values;
            return Response(string.Join('\n', shards.Select(shard => $"{shard.Id}: `{shard.State}`")));
        }


        [RequireBotOwner]
        [SlashCommand("shutdown")]
        [RequireGuild(551567205461131305)]
        [Description("Shutdown the application.")]
        public async Task Shutdown()
        {
            Logger.LogInformation("Shutdown requested");

            await Deferral();

            ShutdownInProgress = true;

            await Context.Bot.SetPresenceAsync(UserStatus.DoNotDisturb);

            Logger.LogInformation("Waiting on commands...");

            await WaitForCommandsAsync(1);

            Logger.LogInformation("Commands completed...");

            await Response("Gooodbye...");

            await ApplicationHost.StopAsync(Context.Bot.StoppingToken);
        }

        [RequireBotOwner]
        [SlashCommand("reboot")]
        [RequireGuild(551567205461131305)]
        [Description("Shutdown and restart the application.")]
        public async Task Reboot([Description("The name of the systemd service")] string serviceName)
        {
            Logger.LogInformation("Reboot requested");

            await Deferral();

            RebootInProgress = true;

            await Context.Bot.SetPresenceAsync(UserStatus.DoNotDisturb);

            Logger.LogInformation("Waiting on commands...");

            await WaitForCommandsAsync(1);

            Logger.LogInformation("Commands completed...");

            await Response("Goodbye...");

            Restart(serviceName);
        }

        [RequireBotOwner]
        [SlashCommand("get-status")]
        [RequireGuild(551567205461131305)]
        [Description("Return the status of the systmd service")]
        public async Task Status([Description("The name of the systemd service")] string serviceName)
        {
            var status = await Systemd.GetServiceStatusAsync($"{serviceName}.service");

            await Response(status);
        }

        private async Task<IResult> GetStatsAsync()
        {
            if (!await Context.Bot.IsOwnerAsync(Context.AuthorId)) return Response("pong");

            var process = Process.GetCurrentProcess();
            var memory = $"{Math.Round((double)process.WorkingSet64 / (1024 * 1024))} MB | {Math.Round((double)process.PagedMemorySize64 / (1024 * 1024))} MB";
            var uptime = (DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime).Humanize(5, true, maxUnit: TimeUnit.Month, minUnit: TimeUnit.Second);

            return Response($"{memory} | {uptime}");
        }

        private void Restart(string serviceName)
        {
            var psi = new ProcessStartInfo("sudo")
            {
                Arguments = $"systemctl restart {serviceName}.service"
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception)
            {
                RebootInProgress = false;

                Logger.LogWarning("Failed to restart service");

            }
        }
    }
}
