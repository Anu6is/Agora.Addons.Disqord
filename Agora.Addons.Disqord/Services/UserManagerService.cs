using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    public class UserManagerService : AgoraService, IUserManager, ICurrentUserService
    {
        private readonly ICommandContextAccessor _accessor;
        private readonly IGuildSettingsService _guildSettingsService;
        
        public EmporiumUser CurrentUser { get; set; }

        public UserManagerService(ILogger<UserManagerService> logger, ICommandContextAccessor accessor) : base(logger)
        {
            _accessor = accessor;
            _guildSettingsService = accessor.Context?.Services.GetRequiredService<IGuildSettingsService>();
        }

        public async ValueTask<bool> IsAdministrator(IEmporiumUser user)
        {
            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));

            if (member == null) return false;
            if (member.GetPermissions().ManageGuild) return true;

            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(_accessor.Context.GuildId.Value);
            var adminRole = guildSettings.AdminRole;

            return member.RoleIds.Contains(adminRole);
        }

        public async ValueTask<bool> IsBroker(IEmporiumUser user)
        {
            if (await IsAdministrator(user)) return true;

            var member = await GetMemberAsync(new Snowflake(user.ReferenceNumber.Value));
            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(_accessor.Context.GuildId.Value);
            var brokerRole = guildSettings.BrokerRole;

            return member.RoleIds.Contains(brokerRole);
        }

        public async ValueTask<bool> IsHost(IEmporiumUser user)
        {
            if (await IsBroker(user)) return true;

            var guildSettings = await _guildSettingsService.GetGuildSettingsAsync(_accessor.Context.GuildId.Value);
            var hostRole = guildSettings.MerchantRole;

            if (hostRole == 0ul || hostRole == _accessor.Context.GuildId.Value) return true;

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
            _accessor.Context.Bot.CacheProvider.TryGetMembers(_accessor.Context.GuildId.Value, out var memberCache);

            if (memberCache.TryGetValue(id, out var cachedMember)) return cachedMember;

            IMember member;

            await using (_accessor.Context.BeginYield())
            {
                if (_accessor.Context.Bot.GetShard(_accessor.Context.GuildId).RateLimiter.GetRemainingRequests() < 3)
                {
                    member = await _accessor.Context.Bot.FetchMemberAsync(_accessor.Context.GuildId.Value, id);
                }
                else
                {
                    var members = await _accessor.Context.Bot.Chunker.QueryAsync(_accessor.Context.GuildId.Value, new[] { id });
                    member = members.GetValueOrDefault(id);
                }
            }

            return member;
        }

        public async ValueTask<EmporiumUser> GetCurrentUserAsync()
        {
            if (CurrentUser != null) return CurrentUser;
            
            var cache = _accessor.Context.Services.GetRequiredService<IEmporiaCacheService>();
            var user = await cache.GetUserAsync(_accessor.Context.GuildId.Value, _accessor.Context.Author.Id);
            
            CurrentUser = EmporiumUser.Create(new EmporiumId(user.EmporiumId), new UserId(user.UserId), ReferenceNumber.Create(user.ReferenceNumber));

            return CurrentUser;
        }
    }
}
