using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Common;
using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Services;
using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
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
    [CheckActiveListings]
    [SlashGroup("auction")]
    [RequireShowroom("Auction")]
    [Description("Add an item to be auctioned")]
    [RequireBotPermissions(Permissions.SendMessages | Permissions.SendEmbeds)]
    public sealed class CreateAuctionModule : AgoraModuleBase
    {
        [SlashGroup("add")]
        public sealed class AuctionCommandGroup : AgoraModuleBase
        {
            private bool _scheduleOverride;
            private (DayOfWeek Weekday, TimeSpan Time)[] _schedule;

            public override async ValueTask OnBeforeExecuted()
            {
                await base.OnBeforeExecuted();

                _scheduleOverride = TryOverrideSchedule(out _schedule);
            }

            [SlashCommand("standard")]
            [RateLimit(10, 1, RateLimitMeasure.Hours, ChannelType.News)]
            [Description("User with the highest bid wins when the auction ends.")]
            public async Task<IResult> CreateStandardAuction(
                [Description("Title of the item to be auctioned."), Maximum(75)] ProductTitle title,
                [Description("Price at which bidding should start at. Numbers only!"), Minimum(0)] double startingPrice,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Length of time the auction should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Sell immediately for this price."), Minimum(0)] double buyNowPrice = 0,
                [Description("Do NOT sell unless bids exceed this price."), Minimum(0)] double reservePrice = 0,
                [Description("Min amount bids can be increased by. Defaults to 1"), Minimum(0)] double minBidIncrease = 0,
                [Description("Max amount bids can be increased by."), Minimum(0)] double maxBidIncrease = 0,
                [Description("Scheduled start of the auction (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Restrict bidding to this role"), RequireRole(AuthorizationRole.Broker)] IRole requiredRole = null,
                [Description("Participation fee required to bid on this listing."), Minimum(0)] double entryFee = 0,
                [Description("Total participants required to start this auction."), Minimum(0)] int requiredEntries = 0,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] [CheckListingLimit] IMember owner = null,
                [Description("True to allow the lowest bid to win")] bool reverseBidding = false,
                [Description("Repost the listing after it ends.")] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Auction);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, maxBidIncrease == 0);

                if (missing.Any()) return Response($"Please include: {string.Join(" & ", missing)}");

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var currentDateTime = emporium.LocalTime.DateTime.AddSeconds(3);
                var defaultDuration = Settings.DefaultDuration == TimeSpan.Zero
                    ? Settings.MinimumDurationDefault
                        ? Settings.MinimumDuration
                        : Settings.MaximumDuration
                    : Settings.DefaultDuration;

                quantity ??= Stock.Create(1);
                scheduledStart ??= currentDateTime;
                currency ??= Settings.DefaultCurrency.Code;
                duration = duration == default ? defaultDuration : duration;

                var defaultMin = CalculateMinIncrease(startingPrice, currency, emporium);
                var scheduledEnd = _scheduleOverride ? currentDateTime.OverrideEndDate(_schedule) : scheduledStart.Value.Add(duration);

                if (_scheduleOverride) scheduledStart = scheduledEnd.OverrideStartDate(currentDateTime, _schedule, duration);

                var startDelay = scheduledStart.Value - currentDateTime;

                var response = await ValidatePremiumRequirementsAsync(entryFee, requiredEntries, startDelay);

                if (response is not null) return response;

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Auction);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));
                var bidIncrease = (decimal)minBidIncrease;

                var item = new AuctionItemModel(title, currency, (decimal)startingPrice, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    ReservePrice = (decimal)reservePrice,
                    MinBidIncrease = bidIncrease < defaultMin ? defaultMin : bidIncrease,
                    MaxBidIncrease = (decimal)maxBidIncrease,
                    Reversed = reverseBidding,
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new StandardAuctionModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    BuyNowPrice = (decimal)buyNowPrice,
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous,
                    Roles = requiredRole is null ? Array.Empty<string>() : new[] { requiredRole.Id.ToString() }
                };

                var result = await Base.ExecuteAsync(new CreateStandardAuctionCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                if (owner is not null) await PluginManagerService.StoreBrokerDetailsAsync(EmporiumId, result.Data.Id, Context.Author.Id);

                if (entryFee > 0) await CreatePremiumAuctionAsync(result.Data, (decimal)entryFee, requiredEntries);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Standard Auction successfully created!");
            }

            [SlashCommand("sealed")]
            [RateLimit(10, 1, RateLimitMeasure.Hours, ChannelType.News)]
            [Description("Bids are hidden. Winner pays the second highest bid.")]
            public async Task<IResult> CreateVickreyAuction(
                [Description("Title of the item to be auctioned."), Maximum(75)] ProductTitle title,
                [Description("Price at which bidding should start at. Numbers only!"), Minimum(0)] double startingPrice,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Length of time the auction should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Limits the number of bids that can be submitted."), Minimum(1)] uint maxParticipants = 0,
                [Description("Do NOT sell unless bids exceed this price."), Minimum(0)] double reservePrice = 0,
                [Description("Min amount bids can be increased by. Defaults to 1"), Minimum(0)] double minBidIncrease = 1,
                [Description("Max amount bids can be increased by."), Minimum(0)] double maxBidIncrease = 0,
                [Description("Scheduled start of the auction (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Restrict bidding to this role"), RequireRole(AuthorizationRole.Broker)] IRole requiredRole = null,
                [Description("Participation fee required to bid on this listing."), Minimum(0)] double entryFee = 0,
                [Description("Total participants required to start this auction."), Minimum(0)] int requiredEntries = 0,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] [CheckListingLimit] IMember owner = null,
                [Description("True to allow the lowest bid to win")] bool reverseBidding = false,
                [Description("Repost the listing after it ends.")] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Auction);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, maxBidIncrease == 0);

                if (missing.Any()) return Response($"Please include: {string.Join(" & ", missing)}");

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var currentDateTime = emporium.LocalTime.DateTime.AddSeconds(3);
                var defaultDuration = Settings.DefaultDuration == TimeSpan.Zero
                    ? Settings.MinimumDurationDefault
                        ? Settings.MinimumDuration
                        : Settings.MaximumDuration
                    : Settings.DefaultDuration;

                quantity ??= Stock.Create(1);
                scheduledStart ??= currentDateTime;
                currency ??= Settings.DefaultCurrency.Code;
                duration = duration == default ? defaultDuration : duration;

                var defaultMin = CalculateMinIncrease(startingPrice, currency, emporium);
                var scheduledEnd = _scheduleOverride ? currentDateTime.OverrideEndDate(_schedule) : scheduledStart.Value.Add(duration);

                if (_scheduleOverride) scheduledStart = scheduledEnd.OverrideStartDate(currentDateTime, _schedule, duration);

                var startDelay = scheduledStart.Value - currentDateTime;

                var response = await ValidatePremiumRequirementsAsync(entryFee, requiredEntries, startDelay);

                if (response is not null) return response;

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Auction);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));
                var bidIncrease = (decimal)minBidIncrease;

                var item = new AuctionItemModel(title, currency, (decimal)startingPrice, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    ReservePrice = (decimal)reservePrice,
                    MinBidIncrease = bidIncrease < defaultMin ? defaultMin : bidIncrease,
                    MaxBidIncrease = (decimal)maxBidIncrease,
                    Reversed = reverseBidding
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new VickreyAuctionModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    MaxParticipants = maxParticipants,
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous,
                    Roles = requiredRole is null ? Array.Empty<string>() : new[] { requiredRole.Id.ToString() }
                };

                var result = await Base.ExecuteAsync(new CreateVickreyAuctionCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                if (owner is not null) await PluginManagerService.StoreBrokerDetailsAsync(EmporiumId, result.Data.Id, Context.Author.Id);

                if (entryFee > 0) await CreatePremiumAuctionAsync(result.Data, (decimal)entryFee, requiredEntries);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Sealed-bid Auction successfully created!");
            }

            [SlashCommand("live")]
            [RateLimit(10, 1, RateLimitMeasure.Hours, ChannelType.News)]
            [Description("Auction ends if no bids are made during the timeout period.")]
            public async Task<IResult> CreateLiveAuction(
                [Description("Title of the item to be auctioned."), Maximum(75)] ProductTitle title,
                [Description("Price at which bidding should start at. Numbers only!"), Minimum(0)] double startingPrice,
                [Description("Max time between bids. Auction ends if no new bids. (example: 5m or 5 minutes)"), RestrictTimeout(5, 432000)] TimeSpan timeout,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Length of time the auction should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Quantity available. Defaults to 1.")] Stock quantity = null,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Sell immediately for this price."), Minimum(0)] double buyNowPrice = 0,
                [Description("Do NOT sell unless bids exceed this price."), Minimum(0)] double reservePrice = 0,
                [Description("Min amount bids can be increased by. Defaults to 1"), Minimum(0)] double minBidIncrease = 1,
                [Description("Max amount bids can be increased by."), Minimum(0)] double maxBidIncrease = 0,
                [Description("Scheduled start of the auction (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Restrict bidding to this role"), RequireRole(AuthorizationRole.Broker)] IRole requiredRole = null,
                [Description("Participation fee required to bid on this listing."), Minimum(0)] double entryFee = 0,
                [Description("Total participants required to start this auction."), Minimum(0)] int requiredEntries = 0,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] [CheckListingLimit] IMember owner = null,
                [Description("True to allow the lowest bid to win")] bool reverseBidding = false,
                [Description("Repost the listing after it ends.")] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Auction);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, maxBidIncrease == 0);

                if (missing.Any()) return Response($"Please include: {string.Join(" & ", missing)}");

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var currentDateTime = emporium.LocalTime.DateTime.AddSeconds(3);
                var defaultDuration = Settings.DefaultDuration == TimeSpan.Zero
                    ? Settings.MinimumDurationDefault
                        ? Settings.MinimumDuration
                        : Settings.MaximumDuration
                    : Settings.DefaultDuration;

                quantity ??= Stock.Create(1);
                scheduledStart ??= currentDateTime;
                currency ??= Settings.DefaultCurrency.Code;
                duration = duration == default ? defaultDuration : duration;

                var defaultMin = CalculateMinIncrease(startingPrice, currency, emporium);
                var scheduledEnd = _scheduleOverride ? currentDateTime.OverrideEndDate(_schedule) : scheduledStart.Value.Add(duration);

                if (_scheduleOverride) scheduledStart = scheduledEnd.OverrideStartDate(currentDateTime, _schedule, duration);

                var startDelay = scheduledStart.Value - currentDateTime;

                var response = await ValidatePremiumRequirementsAsync(entryFee, requiredEntries, startDelay);

                if (response is not null) return response;

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Auction);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));
                var bidIncrease = (decimal)minBidIncrease;

                var item = new AuctionItemModel(title, currency, (decimal)startingPrice, quantity)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    ReservePrice = (decimal)reservePrice,
                    MinBidIncrease = bidIncrease < defaultMin ? defaultMin : bidIncrease,
                    MaxBidIncrease = (decimal)maxBidIncrease,
                    Reversed = reverseBidding
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new LiveAuctionModel(scheduledStart.Value, scheduledEnd, timeout, new UserId(userDetails.UserId))
                {
                    BuyNowPrice = (decimal)buyNowPrice,
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous,
                    Roles = requiredRole is null ? Array.Empty<string>() : new[] { requiredRole.Id.ToString() }
                };

                var result = await Base.ExecuteAsync(new CreateLiveAuctionCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                if (owner is not null) await PluginManagerService.StoreBrokerDetailsAsync(EmporiumId, result.Data.Id, Context.Author.Id);

                if (entryFee > 0) await CreatePremiumAuctionAsync(result.Data, (decimal)entryFee, requiredEntries);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Live Auction successfully created!");
            }

            [AutoComplete("standard")]
            [AutoComplete("sealed")]
            [AutoComplete("live")]
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

            private decimal CalculateMinIncrease(double startingPrice, string currency, CachedEmporium emporium)
            {
                var selectedCurrency = emporium.Currencies.FirstOrDefault(x => x.Matches(currency));
                var defaultMin = selectedCurrency == null ? Settings.DefaultCurrency.MinAmount : selectedCurrency.MinAmount;

                if (Settings.MinBidIncrease.Amount > 0)
                {
                    if (Settings.MinBidIncrease.DeltaType == DeltaType.Percent)
                    {
                        var fraction = Settings.MinBidIncrease.Amount / 100;
                        var minCalc = fraction * (decimal)startingPrice;

                        defaultMin = Math.Max(defaultMin, minCalc);
                    }
                    else
                    {
                        defaultMin = Math.Max(defaultMin, Settings.MinBidIncrease.Amount);
                    }
                }

                return defaultMin;
            }

            private async Task<IResult> ValidatePremiumRequirementsAsync(double entryFee, int requiredEntries, TimeSpan? startDelay)
            {
                if (entryFee == 0) return null;

                var parameters = new PluginParameters() { { "EmporiumId", EmporiumId } };

                var result = await PluginManagerService.ExecutePlugin("EntryFeeValidator", parameters);

                if (!result.IsSuccessful) return Response(result.FailureReason);

                if (requiredEntries > 0 && entryFee == 0)
                    return Response("An entry fee must be set if a minimum number of entries is required!");

                if (requiredEntries > 0 && startDelay < TimeSpan.FromSeconds(55))
                    return Response("The scheduled start time should allow enough time for participants to join.");

                if (entryFee > 0 && !Bot.GetCurrentMember(Guild.Id).CalculateGuildPermissions().HasFlag(Permissions.ManageRoles))
                    return Response("The bot requires `Manage Roles` permissions to create Premium Listings.");

                return null;
            }

            private async Task CreatePremiumAuctionAsync(Listing listing, decimal entryFee, int requiredEntries)
            {
                var parameters = new PluginParameters()
                {
                    { "Listing", listing },
                    { "EntryFee", entryFee },
                    { "RequiredEntries", requiredEntries }
                };

                await PluginManagerService.ExecutePlugin("PremiumListingManager", parameters);
            }
        }
    }
}
