﻿using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Extensions;
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
    [CheckActiveListings]
    [SlashGroup("trade")]
    [RequireShowroom("Trade")]
    [Description("Create a trade request")]
    [RequireBotPermissions(Permissions.SendMessages | Permissions.SendEmbeds | Permissions.ManageThreads)]
    public sealed class CreateTradeModule : AgoraModuleBase
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
            [Description("Specify what you have to offer and what you want in return")]
            public async Task<IResult> CreateStandardTrade(
                [Description("Title of the item to be traded."), Maximum(75)] ProductTitle offering,
                [Description("Title of the item you want in return."), Maximum(75)] string accepting,
                [Description("Length of time the trade should last. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Scheduled start of the auction (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Restrict trades to this role"), RequireRole(AuthorizationRole.Broker)] IRole requiredRole = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)][CheckListingLimit] IMember owner = null,
                [Description("Repost the listing after it ends."), RequireReschedule()] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Trade);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, false);

                if (missing.Any()) return Response($"Please include: {string.Join(" & ", missing)}");

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var currentDateTime = emporium.LocalTime.DateTime.AddSeconds(3);
                var defaultDuration = Settings.DefaultDuration == TimeSpan.Zero
                    ? Settings.MinimumDurationDefault
                        ? Settings.MinimumDuration
                        : Settings.MaximumDuration
                    : Settings.DefaultDuration;

                scheduledStart ??= currentDateTime;
                duration = duration == default ? defaultDuration : duration;

                var scheduledEnd = _scheduleOverride ? currentDateTime.OverrideEndDate(_schedule) : scheduledStart.Value.Add(duration);

                if (_scheduleOverride) scheduledStart = scheduledEnd.OverrideStartDate(currentDateTime, _schedule, duration);

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Trade);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new TradeItemModel<string>(offering, accepting)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                reschedule = Settings.Features.HasFlag(SettingsFlags.DisableRelisting)
                    ? RescheduleOption.Never
                    : reschedule;

                var listing = new StandardTradeModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous,
                    AllowOffers = false,
                    Roles = requiredRole is null ? Array.Empty<string>() : new[] { requiredRole.Id.ToString() }
                };

                var result = await Base.ExecuteAsync(new CreateStandardTradeCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                if (owner is not null) await PluginManagerService.StoreBrokerDetailsAsync(EmporiumId, result.Data.Id, Context.Author.Id);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Standard Trade successfully created!");
            }

            //[SlashCommand("open")]
            [RateLimit(10, 1, RateLimitMeasure.Hours, ChannelType.News)]
            [Description("Specify what you have to offer and allow users to submit a counter offer")]
            public async Task<IResult> CreateOpenTrade(
                [Description("Title of the item to be traded."), Maximum(75)] ProductTitle offering,
                [Description("Title of the preferred item you want in return."), Maximum(75)] string accepting = "Best Offer",
                [Description("Length of time the trade should last. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Scheduled start of the auction (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Restrict trades to this role"), RequireRole(AuthorizationRole.Broker)] IRole requiredRole = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)][CheckListingLimit] IMember owner = null,
                [Description("Repost the listing after it ends."), RequireReschedule()] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Trade);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, false);

                if (missing.Any()) return Response($"Please include: {string.Join(" & ", missing)}");

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var currentDateTime = emporium.LocalTime.DateTime.AddSeconds(3);
                var defaultDuration = Settings.DefaultDuration == TimeSpan.Zero
                    ? Settings.MinimumDurationDefault
                        ? Settings.MinimumDuration
                        : Settings.MaximumDuration
                    : Settings.DefaultDuration;

                scheduledStart ??= currentDateTime;
                duration = duration == default ? defaultDuration : duration;

                var scheduledEnd = _scheduleOverride ? currentDateTime.OverrideEndDate(_schedule) : scheduledStart.Value.Add(duration);

                if (_scheduleOverride) scheduledStart = scheduledEnd.OverrideStartDate(currentDateTime, _schedule, duration);

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Trade);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new TradeItemModel<string>(offering, accepting)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                reschedule = Settings.Features.HasFlag(SettingsFlags.DisableRelisting)
                    ? RescheduleOption.Never
                    : reschedule;

                var listing = new StandardTradeModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous,
                    AllowOffers = true,
                    Roles = requiredRole is null ? Array.Empty<string>() : new[] { requiredRole.Id.ToString() }
                };

                var result = await Base.ExecuteAsync(new CreateStandardTradeCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                if (owner is not null) await PluginManagerService.StoreBrokerDetailsAsync(EmporiumId, result.Data.Id, Context.Author.Id);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Open Trade successfully created!");
            }

            [SlashCommand("request")]
            [RateLimit(10, 1, RateLimitMeasure.Hours, ChannelType.News)]
            [Description("List an item you are searching for")]
            public async Task<IResult> CreateReverseMarket(
                [Description("Title of the item you want."), Maximum(75)] ProductTitle title,
                [Description("Price at which you wish to purchase."), Minimum(0)] double price,
                [Description("Currency to use. Defaults to server default.")] string currency = null,
                [Description("Length of time the itme is available. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("When the request would be available (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the seller."), Maximum(250)] HiddenMessage message = null,
                [Description("Restrict trades to this role"), RequireRole(AuthorizationRole.Broker)] IRole requiredRole = null,
                [Description("Item requester. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)][CheckListingLimit] IMember buyer = null,
                [Description("Repost the listing after it ends."), RequireReschedule()] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item requester.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Market);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, false);

                if (missing.Any()) return Response($"Please include: {string.Join(" & ", missing)}");

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var currentDateTime = emporium.LocalTime.DateTime.AddSeconds(3);
                var defaultDuration = Settings.DefaultDuration == TimeSpan.Zero
                    ? Settings.MinimumDurationDefault
                        ? Settings.MinimumDuration
                        : Settings.MaximumDuration
                    : Settings.DefaultDuration;

                scheduledStart ??= currentDateTime;
                currency ??= Settings.DefaultCurrency.Code;
                duration = duration == default ? defaultDuration : duration;

                var scheduledEnd = _scheduleOverride ? currentDateTime.OverrideEndDate(_schedule) : scheduledStart.Value.Add(duration);

                if (_scheduleOverride) scheduledStart = scheduledEnd.OverrideStartDate(currentDateTime, _schedule, duration);

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Trade);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));
                var cur = emporium.Currencies.First(x => x.Matches(currency));

                var item = new TradeItemModel<Money>(title, Money.Create((decimal)price, cur))
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description
                };

                var ownerId = buyer?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                reschedule = Settings.Features.HasFlag(SettingsFlags.DisableRelisting)
                    ? RescheduleOption.Never
                    : reschedule;

                var listing = new StandardTradeModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous,
                    Roles = requiredRole is null ? Array.Empty<string>() : new[] { requiredRole.Id.ToString() }
                };

                var result = await Base.ExecuteAsync(new CreateCommissionTradeCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                if (buyer is not null) await PluginManagerService.StoreBrokerDetailsAsync(EmporiumId, result.Data.Id, Context.Author.Id);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Request successfully created!");
            }

            [AutoComplete("standard")]
            [AutoComplete("request")]
            //[AutoComplete("open")]
            public async Task AutoComplete(AutoComplete<string> category, AutoComplete<string> subcategory)
            {
                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                if (emporium == null) return;

                if (category.IsFocused)
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
