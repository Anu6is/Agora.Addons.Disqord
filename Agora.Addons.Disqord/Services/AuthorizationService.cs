using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    public class AuthorizationService : AgoraService, IAuthorizationService
    {
        private readonly IUserManager _userManager;

        public AuthorizationService(ILogger<AuthorizationService> logger, IUserManager userManager) : base(logger)
        {
            _userManager = userManager;
        }

        public ValueTask AuthroizeAsync(IEmporiumUser currentUser, IEnumerable<AuthorizeAttribute> authorizeAttributes)
        {
            return default;
        }
        
        public ValueTask<bool> SkipAuthorizationAsync(IEmporiumUser currentUser)
        {
            return ValueTask.FromResult(currentUser != null && currentUser.EmporiumId.Value == currentUser.ReferenceNumber.Value);
        }
    }
}
