using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    public sealed class BotControlModule : AgoraModuleBase
    {
        public IHost ApplicationHost { get; init; }

        public BotControlModule(IHost applicationHost) => ApplicationHost = applicationHost;

        [SlashCommand("ping")]
        [Description("Test application responsiveness.")]
        [RateLimit(1, 5, RateLimitMeasure.Seconds, RateLimitBucketType.Guild)]
        public IResult Ping()
            => Response("pong");

        //[RequireBotOwner]
        [SlashCommand("shutdown")]
        [RequireGuild(551567205461131305)]
        [Description("Shutdown the application.")]
        public async Task Shutdown()
        {
            Logger.LogInformation("Shutdown requested");

            ShutdownInProgress = true;

            await Context.Bot.SetPresenceAsync(UserStatus.DoNotDisturb);

            await WaitForCommandsAsync(1);
            await ApplicationHost.StopAsync(Context.Bot.StoppingToken);
        }

        //[RequireBotOwner]
        [SlashCommand("reboot")]
        [RequireGuild(551567205461131305)]
        [Description("Shutdown and restart the application.")]
        public async Task Reboot()
        {
            Logger.LogInformation("Reboot requested");

            RebootInProgress = true;

            await Context.Bot.SetPresenceAsync(UserStatus.DoNotDisturb);

            await WaitForCommandsAsync(1);
            await ApplicationHost.StopAsync(Context.Bot.StoppingToken);

            var path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            System.Diagnostics.Process.Start(path);
        }
    }
}
