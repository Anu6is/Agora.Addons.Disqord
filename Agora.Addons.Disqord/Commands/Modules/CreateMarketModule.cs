using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Application.Models;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Qmmands;
using Qommon;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    [SlashGroup("market")]
    [RequireShowroom("Market")]
    [RequireBotPermissions(Permissions.SendMessages | Permissions.SendEmbeds | Permissions.ManageThreads)]
    public sealed class CreateMarketModule : AgoraModuleBase
    {
        [SlashGroup("add")]
        public sealed class MarketCommandGroup : AgoraModuleBase
        {
            [SlashCommand("standard")]
            [Description("List an item(s) for sale at a fixed price.")]
            public async Task CreateStandarMarket(
                [Description("Length of time the item is available.")] TimeSpan duration,
                [Description("Title of the item to be sold."), Maximum(75)] ProductTitle title,
                [Description("Price at which the item is being sold."), Minimum(0)] double price,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("When the item would be available. Defaults to now.")] DateTime? scheduledStart = null,
                [Description("The type of discount to aplly.")] Discount discountType = Discount.None,
                [Description("The amount of discount to apply."), Minimum(0)] double discountAmount = 0,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the buyer."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRoleAttribute(AuthorizationRole.Broker)] IMember owner = null,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                quantity ??= Stock.Create(1);
                currency ??= Settings.DefaultCurrency.Symbol;
                scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

                var scheduledEnd = scheduledStart.Value.Add(duration);
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Market);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new MarketItemModel(title, currency, (decimal)price, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new StandardMarketModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    Discount = discountType,
                    DiscountValue = (decimal)discountAmount,
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                await Base.ExecuteAsync(new CreateStandardMarketCommand(showroom, item, listing));

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response("Standard Market successfully created!");
            }

            [SlashCommand("flash")]
            [Description("List an item(s) with a timed discount.")]
            public async Task CreateFlashMarket(
                [Description("Length of time the item is available.")] TimeSpan duration,
                [Description("Title of the item to be sold."), Maximum(75)] ProductTitle title,
                [Description("Price at which the item is being sold."), Minimum(0)] double price,
                [Description("The type of discount to aplly."), Choice("Percent", 1), Choice("Amount", 2)] int discountType,
                [Description("The value of discount to apply."), Minimum(0)] double discountValue,
                [Description("Length of time the discount is available.")] TimeSpan timeout,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("When the item would be available. Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the buyer."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRoleAttribute(AuthorizationRole.Broker)] IMember owner = null,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                quantity ??= Stock.Create(1);
                currency ??= Settings.DefaultCurrency.Symbol;
                scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

                if (timeout > duration) timeout = duration;

                var scheduledEnd = scheduledStart.Value.Add(duration);
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Market);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new MarketItemModel(title, currency, (decimal)price, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new FlashMarketModel(scheduledStart.Value, scheduledEnd, timeout, new UserId(userDetails.UserId))
                {
                    Discount = (Discount)discountType,
                    DiscountValue = (decimal)discountValue,
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                await Base.ExecuteAsync(new CreateFlashMarketCommand(showroom, item, listing));

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response("Flash Market successfully created!");
            }

            [SlashCommand("bulk")]
            [Description("Users can purchase all or a portion of the listed item stock.")]
            public async Task CreateBulkMarket(
                [Description("Length of time the item is available.")] TimeSpan duration,
                [Description("Quantity available. Defaults to 1.")] Stock quantity,
                [Description("Title of the item to be sold."), Maximum(75)] ProductTitle title,
                [Description("Price at which the complete bundle is being sold."), Minimum(0)] double totalPrice,
                [Description("Price of purchasing a single item from the total stock."), Minimum(0)] double pricePerItem,
                [Description("Buying this number of items at once, applies a special price."), Minimum(2)] int amountInBundle = 0,
                [Description("Price applied when purchaing a set number of items at once."), Minimum(0)] double pricePerBundle = 0,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("When the item would be available. Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the buyer."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRoleAttribute(AuthorizationRole.Broker)] IMember owner = null,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                quantity ??= Stock.Create(1);
                currency ??= Settings.DefaultCurrency.Symbol;
                scheduledStart ??= emporium.LocalTime.DateTime.AddSeconds(3);

                var scheduledEnd = scheduledStart.Value.Add(duration);
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Market);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new MarketItemModel(title, currency, (decimal)totalPrice, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new MassMarketModel(scheduledStart.Value, scheduledEnd, (decimal)pricePerItem, new UserId(userDetails.UserId))
                {
                    AmountPerBundle = amountInBundle,
                    CostPerBundle = (decimal)pricePerBundle,
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                await Base.ExecuteAsync(new CreateMassMarketCommand(showroom, item, listing));

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response("Bulk Market successfully created!");
            }

            [AutoComplete("standard")]
            [AutoComplete("flash")]
            [AutoComplete("bulk")]
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
