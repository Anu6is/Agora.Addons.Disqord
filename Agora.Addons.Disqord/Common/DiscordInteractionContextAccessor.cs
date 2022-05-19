using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    
    public class DiscordInteractionContextAccessor : AgoraService, IInteractionContextAccessor
    {
        public DiscordInteractionContext Context { get; set; }
     
        public DiscordInteractionContextAccessor(ILogger<DiscordInteractionContextAccessor> logger) : base(logger) { }
    }
}
