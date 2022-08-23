using Agora.Shared;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qmmands;
using System.Diagnostics;

namespace Agora.Addons.Disqord.Commands
{
    public sealed class BotControlModule : AgoraModuleBase
    {
        public IHost ApplicationHost { get; init; }

        public BotControlModule(IHost applicationHost) => ApplicationHost = applicationHost;

        [SlashCommand("ping")]
        [Description("Test application responsiveness.")]
        [RateLimit(1, 5, RateLimitMeasure.Seconds, RateLimitBucketType.Guild)]
        public async Task<IResult> Ping() => await GetStatsAsync();

        private async Task<IResult> GetStatsAsync()
        {
            if (!await Context.Bot.IsOwnerAsync(Context.AuthorId)) return Response("pong");

            var process = Process.GetCurrentProcess();
            var memory = $"{Math.Round((double)process.WorkingSet64 / (1024 * 1024))} MB | {Math.Round((double)process.PagedMemorySize64 / (1024 * 1024))} MB";
            var uptime = (DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime).Humanize(5, true, maxUnit: TimeUnit.Month, minUnit: TimeUnit.Second);

            return Response($"{memory} | {uptime}");
        }

        //TODO - require bot owner
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

        //TODO - require bot owner
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

            var psi = new ProcessStartInfo("sudo") //TODO - add to config file
            {
                Arguments = $"systemctl restart {serviceName}.service"
            };

            Process.Start(psi);
        }

        //TODO - require bot owner
        [SlashCommand("get-status")]
        [RequireGuild(551567205461131305)]
        [Description("Return the status of the systmd service")]
        public async Task Status([Description("The name of the systemd service")] string serviceName)
        {
            var status = await Systemd.GetServiceStatusAsync($"{serviceName}.service");

            await Response(status);
        }
    }
}
