using Agora.Shared;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
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
        public IResult Ping() => Response("pong");

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
