using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class AgoraProductService : AgoraService, IProductService
    {
        private DiscordBotBase Bot { get; }

        public AgoraProductService(DiscordBotBase bot, ILogger<AgoraProductService> logger) : base(logger)
        {
            Bot = bot;
        }

        public async ValueTask<CachedEmporiumProduct> GetProductAsync(ulong showroomId, ulong productId)
        {

            var message = Bot.GetMessage(showroomId, productId) ?? await Bot.FetchMessageAsync(showroomId, productId) as IUserMessage;

            if (message == null || message.Embeds.Count == 0) return null;

            var embed = message.Embeds[0];
            var owner = embed.Fields.FirstOrDefault(x => x.Name.Equals("Item Owner") || x.Name.Equals("Requester"))?.Value;

            if (owner == null) return null;
            if (!Mention.TryParseUser(owner, out var userId) && !owner.Equals("***Anonymous***")) return null;

            return new CachedEmporiumProduct() { OwnerId = userId, ProductId = productId, ListingType = embed.GetListingType() };
        }
    }
}
