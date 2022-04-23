using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class ProductListingService : AgoraService, IProductListingService
    {
        private readonly DiscordBotBase _agora;
        
        public ProductListingService(DiscordBotBase bot, ILogger<ProductListingService> logger) : base(logger)
        {
            _agora = bot;
        }
        
        public async ValueTask<ReferenceNumber> PostProductListing<TProduct>(EmporiumId emporiumId, ShowroomId showroomId, TProduct product) where TProduct : Product
        {
            var productEmbed = product.ToEmbed();
            var message = await _agora.SendMessageAsync(showroomId.Value, new LocalMessage().AddEmbed(productEmbed));

            return ReferenceNumber.Create(message.Id);
        }
        
        public ValueTask RemoveProductListing(ReferenceNumber referenceNumber)
        {
            throw new NotImplementedException();
        }
    }
}
