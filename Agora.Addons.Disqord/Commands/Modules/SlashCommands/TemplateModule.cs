using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Qmmands;
using Qommon;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [SlashGroup("template")]
    [Description("Create and manage listing templates")]
    [RequireBotPermissions(Permissions.SendMessages | Permissions.SendEmbeds)]
    public sealed class TemplateModule : AgoraModuleBase
    {
        [RequireManager]
        [SlashGroup("add")]
        public sealed class CreateTemplateModule : AgoraModuleBase
        {
            public enum AuctionType { Standard, Sealed, Live }

            public EmporiumTimeParser TimeParser { get; set; }

            public CreateTemplateModule(EmporiumTimeParser parser) => TimeParser = parser;

            [SlashCommand("auction")]
            public async Task<IResult> CreateAuctionTemplate(
                [Description("Type of auction")]AuctionType type,
                [Description("Title of the item"), Maximum(75)] string title = null,
                [Description("Price bidding should start at"), Minimum(0)] double startingPrice = 0,
                [Description("Currency to use")] string currency = null,
                [Description("Length of time the auction should run"), RestrictDuration()] TimeSpan duration = default,
                [Description("Quantity available"), Minimum(1)] int quantity = 0,
                [Description("Attach an image"), RequireContent("image")] IAttachment image = null,
                [Description("Additional information"), Maximum(500)] string description = null,
                [Description("Do NOT sell unless bids exceed this price"), Minimum(0)] double reservePrice = 0,
                [Description("Min amount bids can be increased by"), Minimum(0)] double minBidIncrease = 0,
                [Description("Max amount bids can be increased by"), Minimum(0)] double maxBidIncrease = 0,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under"), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] string message = null,
                [Description("Item owner"), RequireRole(AuthorizationRole.Broker)][CheckListingLimit] IMember owner = null,
                [Description("True to allow the lowest bid to win")] bool reverseBidding = false,
                [Description("Repost the listing after it ends")] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                var template = new AuctionTemplate()
                {
                    Type = type.ToString(),
                    Title = title,
                    StartingPrice = startingPrice,
                    Duration = duration,
                    Quantity = quantity,
                    Image = image?.Url,
                    Description = description,
                    ReservePrice = reservePrice,
                    MinBidIncrease = minBidIncrease,
                    MaxBidIncrease = maxBidIncrease,
                    Category = category,
                    Subcategory = subcategory,
                    Message = message,
                    Owner = owner?.Id ?? 0,
                    ReverseBidding = reverseBidding,
                    Reschedule = reschedule,
                    Anonymous = anonymous         
                };

                currency ??= Settings.DefaultCurrency.Code;
                
                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                
                template.Currency = emporium.Currencies.First(x => x.Matches(currency));

                return View(new AuctionTemplateView(template, TimeParser));
            }

            [AutoComplete("auction")]
            public async Task AutoComplete(AutoComplete<string> currency, AutoComplete<string> category, AutoComplete<string> subcategory)
            {
                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                if (emporium == null) return;

                if (currency.IsFocused)
                {
                    if (emporium.Currencies.Count == 0) return;

                    if (currency.RawArgument == string.Empty)
                        currency.Choices.AddRange(emporium.Currencies.Select(x => x.Code).ToArray());
                    else
                        currency.Choices.AddRange(emporium.Currencies.Select(x => x.Code).Where(s => s.Contains(currency.RawArgument, StringComparison.OrdinalIgnoreCase)).ToArray());
                }
                else if (category.IsFocused)
                {
                    if (emporium.Categories.Count == 0)
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
                                subcategory.Choices.AddRange(
                                    currentCategory.SubCategories.Where(s => s.Title.Value != currentCategory.Title.Value)
                                                                 .Select(x => x.Title.Value)
                                                                 .ToArray());
                            else
                                subcategory.Choices.AddRange(
                                    currentCategory.SubCategories.Where(x => x.Title.Value != currentCategory.Title.Value && x.Title.Value.Contains(subcategory.RawArgument, StringComparison.OrdinalIgnoreCase))
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
