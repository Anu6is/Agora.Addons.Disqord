using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot.Commands;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    public class CurrentUserService : AgoraService, ICurrentUserService
    {
        private readonly IEmporiaCacheService _cache;

        public IMember CurrentMember { get; }
        public EmporiumUser CurrentUser { get; set; }
        
        public CurrentUserService(ICommandContextAccessor commandAccessor, IInteractionContextAccessor interactionAccessor,
                                  IEmporiaCacheService emporiaCacheService, ILogger<CurrentUserService> logger) : base(logger)
        {
            if (commandAccessor.Context is not null && commandAccessor.Context.Author is IMember member)
                CurrentMember = member;
            else if (interactionAccessor.Context is not null)
                CurrentMember = interactionAccessor.Context.Author;

            _cache = emporiaCacheService;
        }

        public async ValueTask<EmporiumUser> GetCurrentUserAsync()
        {
            if (CurrentUser != null) return CurrentUser;

            var user = await _cache.GetUserAsync(CurrentMember.GuildId, CurrentMember.Id);

            CurrentUser = EmporiumUser.Create(new EmporiumId(user.EmporiumId), new UserId(user.UserId), ReferenceNumber.Create(user.ReferenceNumber));

            return CurrentUser;
        }
    }
}
