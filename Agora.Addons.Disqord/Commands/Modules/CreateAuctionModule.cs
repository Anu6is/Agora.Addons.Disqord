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
using Qommon;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    [SlashGroup("auction")]
    [RequireShowroom("Auction")]
    [RequireBotPermissions(Permissions.SendMessages | Permissions.SendEmbeds | Permissions.ManageThreads)]
    public sealed class CreateAuctionModule : AgoraModuleBase
    {
        [SlashGroup("add")]
        public sealed class AuctionCommandGroup : AgoraModuleBase
        {
            [SlashCommand("standard")]
            [Description("User with the highest bid wins when the auction ends.")]
            public async Task CreateStandardAuction(
                [Description("Length of time the auction should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration,
                [Description("Title of the item to be auctioned."), Maximum(75)] ProductTitle title,
                [Description("Price at which bidding should start at. Numbers only!"), Minimum(0)] double startingPrice,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Sell immediately for this price."), Minimum(0)] double buyNowPrice = 0,
                [Description("Do NOT sell unless bids exceed this price."), Minimum(0)] double reservePrice = 0,
                [Description("Min amount bids can be increased by. Defaults to 1"), Minimum(0)] double minBidIncrease = 1,
                [Description("Min amount bids can be increased by."), Minimum(0)] double maxBidIncrease = 0,
                [Description("Scheduled start of the auction. Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] IMember owner = null,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                quantity ??= Stock.Create(1);
                currency ??= Settings.DefaultCurrency.Symbol;
                scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

                var scheduledEnd = scheduledStart.Value.Add(duration);
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Auction);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new AuctionItemModel(title, currency, (decimal)startingPrice, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    ReservePrice = (decimal)reservePrice,
                    MinBidIncrease = (decimal)minBidIncrease,
                    MaxBidIncrease = (decimal)maxBidIncrease
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new StandardAuctionModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    BuyNowPrice = (decimal)buyNowPrice,
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                await Base.ExecuteAsync(new CreateStandardAuctionCommand(showroom, item, listing));

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response("Standard Auction successfully created!");
            }

            [SlashCommand("sealed")]
            [Description("Bids are hidden. Winner pays the second highest bid.")]
            public async Task CreateVickreyAuction(
                [Description("Length of time the auction should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration,
                [Description("Title of the item to be auctioned."), Maximum(75)] ProductTitle title,
                [Description("Price at which bidding should start at. Numbers only!"), Minimum(0)] double startingPrice,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Limits the number of bids that can be submitted."), Minimum(1)] uint maxParticipants = 0,
                [Description("Do NOT sell unless bids exceed this price."), Minimum(0)] double reservePrice = 0,
                [Description("Min amount bids can be increased by. Defaults to 1"), Minimum(0)] double minBidIncrease = 1,
                [Description("Max amount bids can be increased by."), Minimum(0)] double maxBidIncrease = 0,
                [Description("Scheduled start of the auction. Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] IMember owner = null,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                quantity ??= Stock.Create(1);
                currency ??= Settings.DefaultCurrency.Symbol;
                scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

                var scheduledEnd = scheduledStart.Value.Add(duration);
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Auction);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new AuctionItemModel(title, currency, (decimal)startingPrice, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    ReservePrice = (decimal)reservePrice,
                    MinBidIncrease = (decimal)minBidIncrease,
                    MaxBidIncrease = (decimal)maxBidIncrease
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new VickreyAuctionModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    MaxParticipants = maxParticipants,
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                await Base.ExecuteAsync(new CreateVickreyAuctionCommand(showroom, item, listing));

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response("Sealed-bid Auction successfully created!");
            }

            [SlashCommand("live")]
            [Description("Auction ends if no bids are made during the timeout period.")]
            public async Task CreateLiveAuction(
                [Description("Length of time the auction should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration,
                [Description("Title of the item to be auctioned."), Maximum(75)] ProductTitle title,
                [Description("Price at which bidding should start at. Numbers only!"), Minimum(0)] double startingPrice,
                [Description("Max time between bids. Auction ends if no new bids."), RestrictDuration(5, 1800)] TimeSpan timeout,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Do NOT sell unless bids exceed this price."), Minimum(0)] double reservePrice = 0,
                [Description("Min amount bids can be increased by. Defaults to 1"), Minimum(0)] double minBidIncrease = 1,
                [Description("Max amount bids can be increased by."), Minimum(0)] double maxBidIncrease = 0,
                [Description("Scheduled start of the auction. Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] IMember owner = null,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                quantity ??= Stock.Create(1);
                currency ??= Settings.DefaultCurrency.Symbol;
                scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

                var scheduledEnd = scheduledStart.Value.Add(duration);
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Auction);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new AuctionItemModel(title, currency, (decimal)startingPrice, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    ReservePrice = (decimal)reservePrice,
                    MinBidIncrease = (decimal)minBidIncrease,
                    MaxBidIncrease = (decimal)maxBidIncrease
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new LiveAuctionModel(scheduledStart.Value, scheduledEnd, timeout, new UserId(userDetails.UserId))
                {
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                await Base.ExecuteAsync(new CreateLiveAuctionCommand(showroom, item, listing));

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response("Live Auction successfully created!");
            }

            [AutoComplete("standard")]
            [AutoComplete("sealed")]
            [AutoComplete("live")]
            public async Task AutoCompleteAuction(AutoComplete<string> currency, AutoComplete<string> category, AutoComplete<string> subcategory)
            {
                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                if (currency.IsFocused)
                {
                    if (currency.RawArgument == string.Empty)
                        currency.Choices.AddRange(emporium.Currencies.Select(x => x.Symbol).ToArray());
                    else
                        currency.Choices.AddRange(emporium.Currencies.Select(x => x.Symbol).Where(s => s.Contains(currency.RawArgument, StringComparison.OrdinalIgnoreCase)).ToArray());
                }
                else if (category.IsFocused)
                {
                    if (!emporium.Categories.Any())
                        category.Choices.Add("No configured server categories exist.");
                    else
                    {
                        if (category.RawArgument == string.Empty)
                            category.Choices.AddRange(emporium.Categories.Select(x => x.Title.Value).ToArray());
                        else
                            category.Choices.AddRange(emporium.Categories.Where(x => x.Title.Value.Contains(category.RawArgument, StringComparison.OrdinalIgnoreCase))
                                                                         .Select(x => x.Title.Value)
                                                                         .ToArray());
                    }
                }
                else if (subcategory.IsFocused)
                {
                    if (!category.Argument.TryGetValue(out var agoraCategory))
                        subcategory.Choices.Add("Select a category to populate suggestions.");
                    else
                    {
                        var currentCategory = emporium.Categories.FirstOrDefault(x => x.Title.Equals(agoraCategory));

                        if (currentCategory?.SubCategories.Count > 1)
                        {
                            if (subcategory.RawArgument == string.Empty)
                                subcategory.Choices.AddRange(currentCategory.SubCategories.Skip(1).Select(x => x.Title.Value).ToArray());
                            else
                                subcategory.Choices.AddRange(currentCategory.SubCategories.Where(x => x.Title.Value.Contains(subcategory.RawArgument, StringComparison.OrdinalIgnoreCase))
                                                                                          .Select(x => x.Title.Value)
                                                                                          .ToArray());
                        }
                        else
                        {
                            subcategory.Choices.Add($"No configured subcategories exist for {currentCategory}");
                        }
                    }
                }

                return;
            }
        }
    }
}
