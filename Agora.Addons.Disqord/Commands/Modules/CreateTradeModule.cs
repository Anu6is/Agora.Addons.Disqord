using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Application.Models;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
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
            private bool _scheduleOverride;
            private (DayOfWeek Weekday, TimeSpan Time)[] _schedule;

            public override ValueTask OnBeforeExecuted()
            {
                var channel = Context.Bot.GetChannel(Context.GuildId, Context.ChannelId) as CachedTextChannel;

                _scheduleOverride = channel != null
                                    && channel.Topic.IsNotNull()
                                    && channel.Topic.StartsWith("Schedule", StringComparison.OrdinalIgnoreCase);

                if (_scheduleOverride)
                {
                    var schedule = channel.Topic.Replace("Schedule", "", StringComparison.OrdinalIgnoreCase).TrimStart(new[] { ':', ' ' });
                    _schedule = schedule.Split(';')
                        .Select(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Select(x => (Weekday: Enum.Parse<DayOfWeek>(x[0]), Time: TimeOnly.Parse(x[1]).ToTimeSpan()))
                        .OrderBy(x => x.Weekday).ToArray();
                }

                return base.OnBeforeExecuted();
            }

            [SlashCommand("standard")]
            [Description("Specify what you have to offer and what you want in return")]
            public async Task CreateStandardTrade(
                [Description("Title of the item to be traded."), Maximum(75)] ProductTitle offering,
                [Description("Title of the item you want in return."), Maximum(75)] string accepting,
                [Description("Length of time the trade should last. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
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
                var currentDateTime = emporium.LocalTime.DateTime.AddSeconds(3);

                scheduledStart ??= currentDateTime;
                duration = duration == default ? Settings.MaximumDuration : duration;

                var scheduledEnd = _scheduleOverride ? currentDateTime.OverrideEndDate(_schedule) : scheduledStart.Value.Add(duration);

                if (_scheduleOverride) scheduledStart = scheduledEnd.OverrideStartDate(currentDateTime, _schedule, duration);

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
