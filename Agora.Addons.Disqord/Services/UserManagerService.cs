using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    public class UserManagerService : AgoraService, IUserManager
    {
        private Snowflake? GuildId { get; set; }
        private DiscordBotBase Bot { get; }

        private readonly IGuildSettingsService _guildSettingsService;

        public UserManagerService(ICommandContextAccessor commandAccessor, IInteractionContextAccessor interactionAccessor,
                                  IGuildSettingsService guildSettingsService, ILogger<UserManagerService> logger) : base(logger)
        {
            _guildSettingsService = guildSettingsService;

            Bot = commandAccessor.Context?.Bot ?? interactionAccessor.Context?.Bot;
            GuildId = (commandAccessor.Context?.GuildId ?? interactionAccessor.Context?.GuildId);
        }

        public async ValueTask<IResult> IsAdministrator(IEmporiumUser user)
        {
            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));

            if (member == null) return Result.Failure("Unauthorized access: Unable to verify user permissions.");
            if (member.GetGuild() == null) return Result.Failure("Unauthorized access: Unable to verify user permissions.");
            if (member.CalculateGuildPermissions().HasFlag(Permissions.ManageGuild)) return Result.Success();

            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(GuildId.GetValueOrDefault());
            var adminRole = guildSettings.AdminRole;

            if (adminRole == GuildId || member.RoleIds.Contains(adminRole)) return Result.Success();

            return Result.Failure("Unauthorized access: Manager role required.");
        }

        public async ValueTask<IResult> IsBroker(IEmporiumUser user)
        {
            var result = await IsAdministrator(user);

            if (result.IsSuccessful) return result;

            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));
            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(GuildId.GetValueOrDefault());
            var brokerRole = guildSettings.BrokerRole;

            if (brokerRole == GuildId || member.RoleIds.Contains(brokerRole)) return Result.Success();

            return Result.Failure("Unauthorized access: Broker role required.");
        }

        public async ValueTask<IResult> IsHost(IEmporiumUser user)
        {
            var result = await IsBroker(user);

            if (result.IsSuccessful) return result;

            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(GuildId.GetValueOrDefault());
            var hostRole = guildSettings.MerchantRole;

            if (hostRole == 0ul || hostRole == GuildId) return Result.Success();

            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));

            if (member.RoleIds.Contains(hostRole)) return Result.Success();

            return Result.Failure("Unauthorized access: Merchant role required.");
        }

        public async ValueTask<IResult> ValidateBuyerAsync(IEmporiumUser user, IBaseRequest command = null, Func<IEmporiumUser, IBaseRequest, Task<IResult>> criteria = null)
        {
            var result = await IsAdministrator(user);
           
            if (!result.IsSuccessful)
            {
                var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(GuildId.GetValueOrDefault());
                var buyerRole = guildSettings.BuyerRole;
                var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));

                var hasRole = buyerRole == 0ul || buyerRole == GuildId || member.RoleIds.Contains(buyerRole);

                if (!hasRole) return Result.Failure("Unauthorized access: Buyer role required.");
            }

            if (criteria is null) return Result.Success();

            return await criteria(user, command);
        }

        public ValueTask<IResult> ValidateUser(IEmporiumUser user) => Result.Success();

        private async ValueTask<IMember> GetMemberAsync(Snowflake id)
        {
            Bot.CacheProvider.TryGetMembers(GuildId.GetValueOrDefault(), out var memberCache);

            if (memberCache.TryGetValue(id, out var cachedMember)) return cachedMember;

            IMember member;

            if (Bot.ApiClient.GetShard(GuildId).RateLimiter.GetRemainingRequests() < 3)
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

        public void SetGuildId(Snowflake id) => GuildId = id;
    }
}
