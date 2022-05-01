using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Emporia.Extensions.Discord.Features.MessageBroker;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Agora.Addons.Disqord.Services
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class MessageConsumerService : AgoraService, IMessageConsumer
    {
        private readonly IMessageBuilder _messageBuilder;
        
        public MessageConsumerService(IMessageBuilder messageBuilder, ILogger<MessageConsumerService> logger) : base(logger) 
        {
            _messageBuilder = messageBuilder;
        }

        public Task StartAsync(ChannelReader<IEnvelope> reader, CancellationToken cancellationToken = default)
        {
            var executingTask = Task.Run(async () => 
            {
                while (await reader.WaitToReadAsync(cancellationToken))
                {
                    while (reader.TryRead(out var envelope))
                    {
                        await envelope.HandleAsync(_messageBuilder);
                    }
                }
            }, cancellationToken);

            return executingTask;
        }
    }
}
