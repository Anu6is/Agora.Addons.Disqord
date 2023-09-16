using Agora.Addons.Disqord.Commands.Checks;
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
    [SlashGroup("giveaway")]
    [RequireShowroom("Giveaway")]
    [Description("Add an item to be Giveawayed")]
    [RequireBotPermissions(Permissions.SendMessages | Permissions.SendEmbeds)]
    public class CreateGiveawayModule : AgoraModuleBase
    {
        [SlashGroup("add")]
        public sealed class GiveawayCommandGroup : AgoraModuleBase
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
            [Description("List an item to be given to a randomly selected user")]
            public async Task<IResult> CreateStandardGiveaway(
                [Description("Title of the item to be Giveawayed."), Maximum(75)] ProductTitle title,
                [Description("Length of time the Giveaway should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Total amount of winners to select. Defaults to 1"), Minimum(0), Maximum(5)] double winners = 1,
                [Description("Maximum number of available tickets."), Minimum(0)] double maxParticipants = 0,
                [Description("Scheduled start of the Giveaway (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] [CheckListingLimit] IMember owner = null,
                [Description("Repost the listing after it ends.")] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Giveaway);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, false);

                if (missing.Count() != 0) return Response($"Please include: {string.Join(" & ", missing)}");

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

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Giveaway);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new GiveawayItemModel(title)
                {
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    MaxParticipants = (uint)maxParticipants,
                    TotalWinners = (uint)winners,
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new StandardGiveawayModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                var result = await Base.ExecuteAsync(new CreateStandardGiveawayCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Standard Giveaway successfully created!");
            }

            [SlashCommand("raffle")]
            [RateLimit(10, 1, RateLimitMeasure.Hours, ChannelType.News)]
            [Description("Purchase a ticket for a random chance to win.")]
            public async Task<IResult> CreateRaffleGiveaway(
                [Description("Title of the item to be Giveawayed."), Maximum(75)] ProductTitle title,
                [Description("Price at which a ticket is sold for. Numbers only!"), Minimum(0)] double ticketPrice,
                [Description("Currency to use. Defaults to server default")] string currency = null,
                [Description("Length of time the Giveaway should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Attach an image to be included with the listing."), RequireContent("image")] IAttachment image = null,
                [Description("Additional information about the item."), Maximum(500)] ProductDescription description = null,
                [Description("Total amount of winners to select. Defaults to 1"), Minimum(0), Maximum(5)] double winners = 1,
                [Description("Maximum number of available tickets. 0 for unlimited."), Minimum(0)] double maxParticipants = 0,
                [Description("Maximum number of tickets a user can purchase."), Minimum(1)] double maxTicketsPerUser = 1,
                [Description("Scheduled start of the Giveaway (yyyy-mm-dd HH:mm). Defaults to now.")] DateTime? scheduledStart = null,
                [Description("Category the item is associated with"), Maximum(25)] string category = null,
                [Description("Subcategory to list the item under. Requires category."), Maximum(25)] string subcategory = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] HiddenMessage message = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)] [CheckListingLimit] IMember owner = null,
                [Description("Repost the listing after it ends.")] RescheduleOption reschedule = RescheduleOption.Never,
                [Description("True to hide the item owner.")] bool anonymous = false)
            {
                await Deferral(isEphemeral: true);

                var requirements = (DefaultListingRequirements)await SettingsService.GetListingRequirementsAsync(Context.GuildId, ListingType.Giveaway);
                var missing = requirements.Validate(image is null, description is null, category is null, subcategory is null, message is null, false);

                if (missing.Count() != 0) return Response($"Please include: {string.Join(" & ", missing)}");

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

                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Giveaway);
                var emporiumCategory = category == null ? null : emporium.Categories.FirstOrDefault(x => x.Title.Equals(category));
                var emporiumSubcategory = subcategory == null ? null : emporiumCategory?.SubCategories.FirstOrDefault(s => s.Title.Equals(subcategory));

                var item = new GiveawayItemModel(title)
                {
                    TicketPrice = (decimal)ticketPrice,
                    Currency = currency,
                    ImageUrl = image == null ? null : new[] { image.Url },
                    Category = emporiumCategory?.Title,
                    Subcategory = emporiumSubcategory?.Title,
                    Description = description,
                    MaxParticipants = (uint)maxParticipants,
                    TotalWinners = (uint)winners,
                };

                var ownerId = owner?.Id ?? Context.Author.Id;
                var userDetails = await Cache.GetUserAsync(Context.GuildId, ownerId);

                var listing = new RaffleGiveawayModel(scheduledStart.Value, scheduledEnd, new UserId(userDetails.UserId))
                {
                    MaxTicketsPerUser = (uint)maxTicketsPerUser,
                    RescheduleOption = reschedule,
                    HiddenMessage = message,
                    Anonymous = anonymous
                };

                var result = await Base.ExecuteAsync(new CreateRaffleGiveawayCommand(showroom, item, listing));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                return Response("Raffle Giveaway successfully created!");
            }

            [AutoComplete("standard")]
            [AutoComplete("raffle")]
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
