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

            if (bot.ApiClient.GetShard(guildId).RateLimiter.GetRemainingRequests() < 3)
                return await bot.FetchMemberAsync(guildId, userId);

            var members = await bot.Chunker.QueryAsync(guildId, new[] { userId });

            return members.GetValueOrDefault(userId);
        }

        public static string ValidateChannelPermissions(this DiscordBotBase bot, ulong guildId, ulong channelId, bool isLogChannel = false)
        {
            var currentMember = bot.GetCurrentMember(guildId);
            var channel = bot.GetChannel(guildId, channelId);

            if (channel == null)
                return $"{AgoraEmoji.RedCrossMark} | Unable to verify channel permissions!";

            var channelPerms = currentMember.CalculateChannelPermissions(channel);

            var basePerms = Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds;
            var permissions = isLogChannel
                ? basePerms
                : channel switch
                {
                    ITextChannel => basePerms | Permissions.ReadMessageHistory |
                                    Permissions.CreatePublicThreads | Permissions.SendMessagesInThreads | Permissions.ManageThreads,
                    ICategoryChannel => basePerms | Permissions.ManageChannels | Permissions.ManageMessages,
                    IForumChannel => basePerms | Permissions.SendMessagesInThreads | Permissions.ManageChannels,
                    _ => basePerms
                };

            if (channel is ITextChannel textChannel && textChannel.IsNews) permissions |= Permissions.ManageMessages;

            if (!channelPerms.HasFlag(permissions))
                return $"{channel.Mention}{AgoraEmoji.RedCrossMark} | Missing channel permissions: **{permissions & ~channelPerms}**";

            return $"{channel.Mention}{AgoraEmoji.GreenCheckMark} | Channel correctly configured!";
        }
    }
}
