using Agora.Addons.Conversion;
using Agora.Addons.Disqord.Checks;
using Agora.Shared;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.DependencyInjection;
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
        private ILoggingLevelSwitcher _switcher;

        public BotControlModule(IHost applicationHost, ILoggingLevelSwitcher switcher)
        {
            ApplicationHost = applicationHost;
            _switcher = switcher;
        }

        [SlashCommand("ping")]
        [Description("Test application responsiveness.")]
        [RateLimit(1, 5, RateLimitMeasure.Seconds, RateLimitBucketType.Guild)]
        public async Task<IResult> Ping() => await GetStatsAsync();

        [RequireBotOwner]
        [SlashCommand("log")]
        [RequireGuild(551567205461131305)]
        [Description("Set the log level")]
        public async Task ChangeLogLevel([Description("Serilog log level")]LogEventLevel logLevel)
        {
            _switcher.SetMinimumLevel(logLevel);

            Logger.LogTrace("ENABLED");
            Logger.LogDebug("ENABLED");
            Logger.LogInformation("ENABLED");

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

        [RequireBotOwner]
        [SkipAuthentication]
        [SlashCommand("convert")]
        [RequireGuild(551567205461131305)]
        [Description("Run the conversion process")]
        public async Task RunConversion([Description("The name of the service to convert to")] string serviceName) 
        {
            RebootInProgress = true;

            Logger.LogInformation("Starting conversion");

            await Deferral();
            await Context.Bot.SetPresenceAsync(UserStatus.DoNotDisturb, new LocalActivity("Maintenance: System Conversion", ActivityType.Playing));
            
            var convertedCount = await Bot.Services.GetRequiredService<ConversionService>().ConvertAsync(Context);

            Logger.LogInformation("Conversion completed. {COUNT} guilds processed", convertedCount);

            Restart(serviceName);
        }

        private async Task<IResult> GetStatsAsync()
        {
            if (!await Context.Bot.IsOwnerAsync(Context.AuthorId)) return Response("pong");

            var process = Process.GetCurrentProcess();
            var memory = $"{Math.Round((double)process.WorkingSet64 / (1024 * 1024))} MB | {Math.Round((double)process.PagedMemorySize64 / (1024 * 1024))} MB";
            var uptime = (DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime).Humanize(5, true, maxUnit: TimeUnit.Month, minUnit: TimeUnit.Second);

            return Response($"{memory} | {uptime}");
        }

        private async void Restart(string serviceName)
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

            await Response("Conversion complete!");
        }
    }
}
