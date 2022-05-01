using Agora.Shared.Services;
using Disqord.Bot;
using Emporia.Extensions.Discord.Features.MessageBroker;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class MessageProcessingService : AgoraService, IMessageBuilder
    {
        private readonly DiscordBotBase _agora;

        public MessageProcessingService(DiscordBotBase bot, ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
        }              
    }
}
