using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;

namespace Agora.Addons.Disqord.Extensions
{
    public static class DiscordBotExtensions
    {
        public static async Task<IMember> GetOrFetchMemberAsync(this DiscordBotBase bot, Snowflake guildId, Snowflake userId)
        {
            bot.CacheProvider.TryGetMembers(guildId, out var memberCache);

            if (memberCache.TryGetValue(userId, out var cachedMember)) 
                return cachedMember;

            if (bot.GetShard(guildId).RateLimiter.GetRemainingRequests() < 3)
                return await bot.FetchMemberAsync(guildId, userId);
                        
            var members = await bot.Chunker.QueryAsync(guildId, new[] { userId });
            
            return members.GetValueOrDefault(userId);
        }
    }
}
