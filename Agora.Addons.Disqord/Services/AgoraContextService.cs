using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord.Bot.Commands.Application;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Singleton)]
    public class AgoraContextService : AgoraService
    {
        public IDiscordApplicationGuildCommandContext Context { get; set; }

        public AgoraContextService(ILogger<AgoraContextService> logger) : base(logger) { }
    }
}
