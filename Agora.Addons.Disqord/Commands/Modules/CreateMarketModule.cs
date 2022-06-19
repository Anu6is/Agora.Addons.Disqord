using Agora.Addons.Disqord.Checks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Features.Commands;
using Emporia.Application.Models;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    [SlashGroup("add")]
    [RequireBotPermissions(Permission.SendMessages | Permission.SendEmbeds | Permission.ManageThreads)]
    public sealed class CreateMarketModule : AgoraModuleBase
    {
        [SlashCommand("standard-market")]
        [Description("List an item(s) for sale at a fixed price.")]
        public async Task CreateStandarMarket(
            [Description("Length of time the item is available.")] TimeSpan duration,
            [Description("Title of the item to be sold.")] ProductTitle title,
            [Description("Price at which the item is being sold.")] decimal price,
            [Description("Currency to use. Defaults to server default")] string currency = null,
            [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
            [Description("Url of image to include. Can also be attached.")] string imageUrl = null,
            [Description("Additional information about the item.")] ProductDescription description = null,
            [Description("When the item would be available. Defaults to now.")] DateTime? scheduledStart = null,
            [Description("The type of discount to aplly.")] Discount discountType = Discount.None,
            [Description("The amount of discount to apply.")] decimal discountAmount = 0,
            [Description("Category the item is associated with")] AgoraCategory category = null,
            [Description("Subcategory to list the item under. Requires category.")] AgoraSubcategory subcategory = null,
            [Description("A hidden message to be sent to the buyer.")] HiddenMessage message = null,
            [Description("Item owner. Defaults to the command user.")] IMember owner = null,
            [Description("True to hide the item owner.")] bool anonymous = false)
        {
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

            quantity ??= Stock.Create(1);
            currency ??= Settings.DefaultCurrency.Symbol;
            scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

            var scheduledEnd = scheduledStart.Value.Add(duration);
            var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Market);
            var item = new MarketItemModel(title, currency, price, quantity)
            {
                ImageUrl = imageUrl,
                Category = category?.ToDomainObject(),
                Subcategory = subcategory?.ToDomainObject(),
                Description = description
            };

            var ownerId = owner?.Id ?? Context.Author.Id;
            var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

            var listing = new StandardMarketModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
            {
                Discount = discountType,
                DiscountValue = discountAmount,
                HiddenMessage = message,
                Anonymous = anonymous
            };

            await Base.ExecuteAsync(new CreateStandardMarketCommand(showroom, item, listing));

            _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));
        }

        //[Description("List an item(s) with a limited/timed discount.")]
        //public async Task CreateFlashMarket()
        //{
        //    throw new NotImplementedException();
        //}

        //[Description("Users can purchase or portion of the listed item stock.")]
        //public async Task CreateBulkMarket()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
