using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    public class UserManagerService : AgoraService, IUserManager
    {
        private Snowflake? GuildId { get; }
        private DiscordBotBase Bot { get; }

        private readonly IGuildSettingsService _guildSettingsService;

        public UserManagerService(ICommandContextAccessor commandAccessor, IInteractionContextAccessor interactionAccessor,
                                  IGuildSettingsService guildSettingsService, ILogger<UserManagerService> logger) : base(logger)
        {
            _guildSettingsService = guildSettingsService;

            Bot = commandAccessor.Context?.Bot ?? interactionAccessor.Context?.Bot;
            GuildId = (commandAccessor.Context?.GuildId ?? interactionAccessor.Context?.GuildId);
        }

        public async ValueTask<bool> IsAdministrator(IEmporiumUser user)
        {
            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));

            if (member == null) return false;
            if (member.GetPermissions().ManageGuild) return true;

            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(GuildId.GetValueOrDefault());
            var adminRole = guildSettings.AdminRole;

            return member.RoleIds.Contains(adminRole);
        }

        public async ValueTask<bool> IsBroker(IEmporiumUser user)
        {
            if (await IsAdministrator(user)) return true;

            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));
            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(GuildId.GetValueOrDefault());
            var brokerRole = guildSettings.BrokerRole;

            return member.RoleIds.Contains(brokerRole);
        }

        public async ValueTask<bool> IsHost(IEmporiumUser user)
        {
            if (await IsBroker(user)) return true;

            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(GuildId.GetValueOrDefault());
            var hostRole = guildSettings.MerchantRole;

            if (hostRole == 0ul || hostRole == GuildId) return true;

            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));

            return member.RoleIds.Contains(hostRole);
        }

        public ValueTask<bool> ValidateBuyer(IEmporiumUser user)
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> ValidateUser(IEmporiumUser user) => ValueTask.FromResult(true);

        private async ValueTask<IMember> GetMemberAsync(Snowflake id)
        {
            Bot.CacheProvider.TryGetMembers(GuildId.GetValueOrDefault(), out var memberCache);

            if (memberCache.TryGetValue(id, out var cachedMember)) return cachedMember;

            IMember member;

            if (Bot.GetShard(GuildId).RateLimiter.GetRemainingRequests() < 3)
            {
                member = await Bot.FetchMemberAsync(GuildId.GetValueOrDefault(), id);
            }
            else
            {
                var members = await Bot.Chunker.QueryAsync(GuildId.GetValueOrDefault(), new[] { id });
                member = members.GetValueOrDefault(id);
            }

            return member;
        }
    }
}
