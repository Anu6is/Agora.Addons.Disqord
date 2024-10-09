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

            var result = message.Embeds
                                .Select((embed, index) => new
                                {
                                    Index = index,
                                    Owner = embed.Fields
                                        .FirstOrDefault(field => field.Name.Equals("Item Owner") || field.Name.Equals("Requester"))
                                        ?.Value
                                })
                                .FirstOrDefault(x => x.Owner != null);

            if (result?.Owner is null) return null;
            if (!Mention.TryParseUser(result.Owner, out var userId) && !result.Owner.Equals("***Anonymous***")) return null;

            return new CachedEmporiumProduct() { OwnerId = userId, ProductId = productId, ListingType = message.Embeds[result.Index].GetListingType() };
        }
    }
}
