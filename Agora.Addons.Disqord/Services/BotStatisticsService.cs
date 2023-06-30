using Agora.Shared;
using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord.Bot;
using Disqord.Gateway;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class BotStatisticsService : AgoraService, IBotStatisticsService
    {
        private DiscordBotBase Bot { get; }

        public BotStatisticsService(DiscordBotBase bot, ILogger<BotStatisticsService> logger) : base(logger) => Bot = bot;

        public int GetTotalGuilds() => Bot.GetGuilds().Count;

        public int GetTotalMembers() => Bot.GetGuilds().Values.Sum(guild => guild.MemberCount);
    }
}
