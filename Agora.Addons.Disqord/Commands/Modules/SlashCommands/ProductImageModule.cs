using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Disqord;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Qmmands;
using IServiceResult = Emporia.Domain.Services.IResult;
using ServiceResult = Emporia.Domain.Services.Result;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    [SlashGroup("image")]
    public sealed class ProductImageModule : AgoraModuleBase
    {
        [SlashCommand("upload")]
        [RequireBarterChannel]
        [Description("Upload images to an existing listing")]
        public async Task<IResult> AttachImages(
            [Description("An image to include with the listing"), RequireContent("image")] IAttachment image1,
            [Description("An image to include with the listing"), RequireContent("image")] IAttachment image2 = null,
            [Description("An image to include with the listing"), RequireContent("image")] IAttachment image3 = null,
            [Description("An image to include with the listing"), RequireContent("image")] IAttachment image4 = null)
        {
            await Deferral(isEphemeral: true);

            var listing = await GetListingTypeAsync();
            var images = new List<string>() { image1.Url };

            if (image2 != null) images.Add(image2.Url);
            if (image3 != null) images.Add(image3.Url);
            if (image4 != null) images.Add(image4.Url);

            return await UpdateImagesAsync(listing, images);
        }

        [SlashCommand("link")]
        [RequireBarterChannel]
        [Description("Add images to an existing listing using urls")]
        public async Task<IResult> LinkImages(
            [Description("An image to include with the listing"), RequireContent("image")] string image1,
            [Description("An image to include with the listing"), RequireContent("image")] string image2 = null,
            [Description("An image to include with the listing"), RequireContent("image")] string image3 = null,
            [Description("An image to include with the listing"), RequireContent("image")] string image4 = null)
        {
            await Deferral(isEphemeral: true);

            var listing = await GetListingTypeAsync();
            var images = new List<string>() { image1 };

            if (image2 != null) images.Add(image2);
            if (image3 != null) images.Add(image3);
            if (image4 != null) images.Add(image4);

            return await UpdateImagesAsync(listing, images);
        }

        private async Task<string> GetListingTypeAsync()
        {
            var listing = string.Empty;
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
            var rooms = emporium.Showrooms.Where(x => x.Id.Value == ShowroomId.Value).Select(x => x.ListingType).ToArray();

            if (rooms.Length == 0) throw new InvalidOperationException("Room not found in </server rooms:1013361602499723275> list");

            if (rooms.Length == 1) listing = rooms[0];
            else
            {
                var product = Cache.GetCachedProduct(EmporiumId.Value, Channel is IThreadChannel thread ? thread.Id : Context.ChannelId);

                if (product == null) throw new InvalidOperationException("Unable to retrieve product details.");

                listing = product.ListingType;
            }

            return listing;
        }

        private async Task<IResult> UpdateImagesAsync(string listing, List<string> images)
        {
            var product = Cache.GetCachedProduct(EmporiumId.Value, Channel is IThreadChannel thread ? thread.Id : Context.ChannelId);

            IServiceResult result = listing switch
            {
                "Auction" => await Base.ExecuteAsync(
                    new UpdateAuctionItemCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(product.ProductId))
                    {
                        ImageUrls = images.ToArray()
                    }),
                "Market" => await Base.ExecuteAsync(
                    new UpdateMarketItemCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(product.ProductId))
                    {
                        ImageUrls = images.ToArray()
                    }),
                "Trade" => await Base.ExecuteAsync(
                    new UpdateTradeItemCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(product.ProductId))
                    {
                        ImageUrls = images.ToArray()
                    }),
                "Giveaway" => await Base.ExecuteAsync(
                    new UpdateGiveawayItemCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(product.ProductId))
                    {
                        ImageUrls = images.ToArray()
                    }),
                _ => ServiceResult.Failure("Unable to update listing.")
            }; 

            if (result.IsSuccessful)
                return Response(new LocalInteractionMessageResponse().WithContent($"Successfully uploaded {images.Count} image(s)").WithIsEphemeral());
            else
                return Response(new LocalInteractionMessageResponse().WithContent(result.FailureReason).WithIsEphemeral());
        }
    }
}
