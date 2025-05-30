using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Cache;
using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Extensions.Interactivity;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Application.Models;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    public sealed partial class TemplateModule : AgoraModuleBase
    {
        [RequireManager]
        [SlashGroup("add")]
        [Description("Add an auction listing from a template")]
        public sealed class PostTemplateModule : AgoraModuleBase
        {
            private bool _scheduleOverride;
            private (DayOfWeek Weekday, TimeSpan Time)[] _schedule;

            private IEnumerable<AuctionTemplate> AuctionTemplates { get; set; }
            private ITemplateCacheService TemplateCacheService { get; set; }

            public PostTemplateModule(TemplateCacheService templateCache) => TemplateCacheService = templateCache;

            public override async ValueTask OnBeforeExecuted()
            {
                await base.OnBeforeExecuted();

                _scheduleOverride = TryOverrideSchedule(out _schedule);
            }

            [SlashCommand("auction")]
            [RequireShowroom("Auction")]
            [Description("Add an auction listing based on the specified template")]
            public async Task<IResult> PostAuctionTemplates(
                [Description("The template to add")] string template,
                [Description("Title of the item to be auctioned."), Maximum(75)] string title = null,
                [Description("Price at which bidding should start at. Numbers only!"), Minimum(0)] double startingPrice = 0,
                [Description("Length of time the auction should run. (example: 7d or 1 week)"), RestrictDuration()] TimeSpan duration = default,
                [Description("Quantity available. Defaults to 1.")] int quantity = 0,
                [Description("Additional information about the item."), Maximum(500)] string description = null,
                [Description("A hidden message to be sent to the winner."), Maximum(250)] string message = null,
                [Description("Restrict bidding to this role"), RequireRole(AuthorizationRole.Broker)] IRole requiredRole = null,
                [Description("Item owner. Defaults to the command user."), RequireRole(AuthorizationRole.Broker)][CheckListingLimit] IMember owner = null,
                [Description("Max time between bids. Auction ends if no new bids. (example: 5m or 5 minutes)"), RestrictTimeout(5, 432000)] TimeSpan timeout = default)
            {
                AuctionTemplates = await TemplateCacheService.GetAuctionTemplatesAsync(Context.GuildId);

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var auctionTemplate = AuctionTemplates.FirstOrDefault(x => x.Name.Equals(template, StringComparison.OrdinalIgnoreCase));

                if (auctionTemplate is null) return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription("Invalid Selection: Template not found."));

                if (!Settings.AllowedListings.Any(listing => listing.Equals($"{auctionTemplate.Type} Auction", StringComparison.OrdinalIgnoreCase)))
                    return ErrorResponse(embeds: new LocalEmbed().WithDescription($"{auctionTemplate.Type} Auctions are not allowed.{Environment.NewLine}Configure Allowed Listings using the </server settings:1013361602499723275> command."));

                auctionTemplate.Owner = auctionTemplate.Owner == 0 ? Context.AuthorId : auctionTemplate.Owner;

                auctionTemplate.Title = title ?? auctionTemplate.Title;
                auctionTemplate.StartingPrice = startingPrice == 0 ? auctionTemplate.StartingPrice : startingPrice;
                auctionTemplate.Duration = duration == default ? auctionTemplate.Duration : duration;
                auctionTemplate.Timeout = timeout == default ? auctionTemplate.Timeout : timeout;
                auctionTemplate.Quantity = quantity == 0 ? auctionTemplate.Quantity : quantity;
                auctionTemplate.Description = description ?? auctionTemplate.Description;
                auctionTemplate.Message = message ?? auctionTemplate.Message;
                auctionTemplate.Owner = owner == null ? auctionTemplate.Owner : owner.Id;

                auctionTemplate.Currency ??= Settings.DefaultCurrency;

                if (auctionTemplate.MinBidIncrease == 0) auctionTemplate.MinBidIncrease = (double)auctionTemplate.Currency.MinAmount;

                var defaultDuration = Settings.DefaultDuration == TimeSpan.Zero
                    ? Settings.MinimumDurationDefault
                        ? Settings.MinimumDuration
                        : Settings.MaximumDuration
                    : Settings.DefaultDuration;

                if (auctionTemplate.Duration == default) auctionTemplate.Duration = defaultDuration;

                if (auctionTemplate.Title.IsNull() || auctionTemplate.StartingPrice <= 0)
                {
                    var modal = new LocalInteractionModalResponse()
                        .WithCustomId(Context.Interaction.Id.ToString())
                        .WithTitle("Missing Values")
                        .WithComponents(
                            LocalComponent.Row(LocalComponent.TextInput("title", "Title", TextInputComponentStyle.Short).WithIsRequired(true).WithPrefilledValue(auctionTemplate.Title)),
                            LocalComponent.Row(LocalComponent.TextInput("startingPrice", "Starting Price", TextInputComponentStyle.Short).WithIsRequired(true).WithPrefilledValue(auctionTemplate.StartingPrice.ToString())));

                    await Context.Interaction.Response().SendModalAsync(modal);

                    var reply = await Context.WaitForInteractionAsync(x => 
                    {
                        if (x.Interaction is not IModalSubmitInteraction modal) return false;
                        if (modal.CustomId != Context.Interaction.Id.ToString()) return false;
                        return true;
                    } , timeout:TimeSpan.FromSeconds(15), cancellationToken: CancellationToken.None);

                    if (reply == null && !auctionTemplate.Validate(out var err))
                        return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription(err));

                    var response = (IModalSubmitInteraction)reply.Interaction;
                    var values = response.Components.OfType<IRowComponent>()
                                       .Select(row => row.Components.OfType<ITextInputComponent>().First().Value);

                    var titleInput = values.FirstOrDefault();
                    var startingPriceInput = values.LastOrDefault();

                    if (!string.IsNullOrWhiteSpace(titleInput)) auctionTemplate.Title = titleInput;
                    if (double.TryParse(startingPriceInput, out var price) && price > 0) auctionTemplate.StartingPrice = price;

                    await response.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Values updated").WithIsEphemeral());
                }
                else
                {
                    await Deferral(true);

                }

                if (!auctionTemplate.Validate(out var error)) return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription(error));

                var user = await Cache.GetUserAsync(Context.GuildId, auctionTemplate.Owner);
                var scheduledStart = emporium.LocalTime.DateTime.AddSeconds(3);
                var scheduledEnd = scheduledStart.Add(auctionTemplate.Duration);

                if (_scheduleOverride)
                {
                    scheduledStart = scheduledStart.OverrideStartDate(scheduledStart, _schedule, auctionTemplate.Duration);
                    scheduledEnd = scheduledEnd.OverrideEndDate(_schedule);
                }

                var item = auctionTemplate.MapToItemModel();
                var showroom = new ShowroomModel(EmporiumId, ShowroomId, ListingType.Auction);
                var listing = auctionTemplate.MapToListingModel(scheduledStart, scheduledEnd, new UserId(user.UserId));

                if (requiredRole is not null) listing.Roles = new[] { requiredRole.Id.ToString() };

                Emporia.Domain.Services.IResult<Listing> result = listing switch
                {
                    StandardAuctionModel => await Base.ExecuteAsync(new CreateStandardAuctionCommand(showroom, item, (StandardAuctionModel)listing)),
                    VickreyAuctionModel => await Base.ExecuteAsync(new CreateVickreyAuctionCommand(showroom, item, (VickreyAuctionModel)listing)),
                    LiveAuctionModel => await Base.ExecuteAsync(new CreateLiveAuctionCommand(showroom, item, (LiveAuctionModel)listing)),
                    _ => null
                };

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                _ = Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                //var success = new LocalInteractionMessageResponse().WithContent("Auction successfully created!").WithIsEphemeral();
                //await Context.Interaction.SendMessageAsync(success);

                return Response("Auction successfully created!");
            }

            [AutoComplete("auction")]
            public async Task AutoComplete(AutoComplete<string> template)
            {
                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

                if (emporium == null) return;

                AuctionTemplates = await TemplateCacheService.GetAuctionTemplatesAsync(Context.GuildId);

                if (template.IsFocused)
                {
                    if (!AuctionTemplates.Any()) return;

                    if (template.RawArgument == string.Empty)
                        template.Choices.AddRange(AuctionTemplates.Select(x => x.Name).ToArray());
                    else
                        template.Choices.AddRange(AuctionTemplates.Select(x => x.Name).Where(s => s.Contains(template.RawArgument, StringComparison.OrdinalIgnoreCase)).ToArray());
                }

                return;
            }
        }
    }
}
