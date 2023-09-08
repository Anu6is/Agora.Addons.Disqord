using Agora.Shared;
using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Gateway.Api;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class DisqordBotService : AgoraService, IDiscordBotService
    {
        private DiscordBotBase Bot { get; }

        public DisqordBotService(DiscordBotBase bot, ILogger<DisqordBotService> logger) : base(logger) => Bot = bot;

        public int GetTotalGuilds() => Bot.GetGuilds().Count;

        public int GetTotalMembers() => Bot.GetGuilds().Values.Sum(guild => guild.MemberCount);

        public int GetShardState(int index)
        {
            if (index < 0 || index >= Bot.ApiClient.Shards.Count) return 0;

            return (int)Bot.ApiClient.Shards[ShardId.FromIndex(index)].State;
        }

        public IEnumerable<ulong> GetMutualGuilds(IEnumerable<ulong> userGuilds)
        {
            var botGuilds = Bot.GetGuilds().Keys.Select(guildId => (ulong)guildId);

            return botGuilds.Intersect(userGuilds);
        }
    }
}
