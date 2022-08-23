using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Application.Models;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    [SlashGroup("trade")]
    [RequireShowroom("Trade")]
    [RequireBotPermissions(Permissions.SendMessages | Permissions.SendEmbeds | Permissions.ManageThreads)]
    public sealed class CreateTradeModule : AgoraModuleBase
    {
        [SlashGroup("add")]
        public sealed class AuctionCommandGroup : AgoraModuleBase
        {
            [SlashCommand("standard")]
            [Description("Specify what you have to offer and what you want in return")]
            public async Task CreateStandardTrade(
                [Description("Length of time the trade should last. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration,
                [Description("Title of the item to be traded."), Maximum(75)] ProductTitle offering,
                [Description("Title of the item you want in return."), Maximum(75)] string accepting,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Scheduled start of the auction. Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] IMember owner = null,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

                var scheduledEnd = scheduledStart.Value.Add(duration);
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Trade);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new TradeItemModel(offering, accepting)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new StandardTradeModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    HiddenMessage = message,
                    Anonymous = anonymous,
                    AllowOffers = false,
                };

                await Base.ExecuteAsync(new CreateStandardTradeCommand(showroom, item, listing));

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response("Standard Trade successfully created!");
            }

            //open
            //Specify what you have to offer and accept the best conunter-offer

            //reverse 
            //Specify what you are looking for and accept best proposal
        }
    }
}
