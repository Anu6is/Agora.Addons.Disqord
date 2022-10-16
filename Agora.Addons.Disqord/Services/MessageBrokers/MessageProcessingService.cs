using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public sealed class MessageProcessingService : AgoraService, IMessageService
    {
        private readonly DiscordBotBase _agora;

        public MessageProcessingService(DiscordBotBase bot, ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
        }

        public async Task<ulong> SendMesssageAsync(ulong channelId, string message)
        {
            var sentMessage = await _agora.SendMessageAsync(channelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithDefaultColor()));
            return sentMessage.Id;
        }

        public async Task<ulong> SendDirectMessageAsync(ulong userId, string message)
        {
            try
            {
                var directChannel = await _agora.CreateDirectChannelAsync(userId);
                var sentMessage = await directChannel.SendMessageAsync(new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithDefaultColor()));

                return sentMessage.Id;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public string GetMessageUrl(ulong guildId, ulong channelId, ulong messageId) => $"https://discordapp.com/channels/{guildId}/{channelId}/{messageId}";
    }
}