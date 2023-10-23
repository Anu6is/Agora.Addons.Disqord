using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Bot.Commands.Interaction;
using Disqord.Extensions.Interactivity;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Application.Features.Queries;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Humanizer;
using Qmmands;
using System.Text;
using System.Text.Json;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    public sealed class ProductListingModule : AgoraModuleBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUserManager _userManager;
        private readonly Random _random;

        public ProductListingModule(Random random, IUserManager userManager, IHttpClientFactory clientFactory)
        {
            _random = random;
            _userManager = userManager;
            _httpClientFactory = clientFactory;
        }

        [MessageCommand("Reroll Giveaway")]
        [Description("Select a new winner for the giveaway award")]
        public async Task<IResult> RerollGiveaway(IUserMessage message)
        {
            await Deferral(true);

            var responseEmbed = new LocalEmbed().WithDefaultColor();
            var response = ValidateResultMessage(message, responseEmbed);

            if (response != null) return response;

            var embed = message.Embeds[0];

            if (!embed.Title.Contains("Giveaway") || message.Attachments.Count == 0)
                return Response(responseEmbed.WithDescription("Command can only be executed on a successful Giveaway result!"));

            var attachment = message.Attachments[0];
            var logContent = await GetLogContentAsync(attachment.Url);

            if (logContent == null) return ErrorResponse(embeds: responseEmbed.WithDescription("Error: Unable to retrieve giveaway results"));

            _ = Mention.TryParseUser(embed.Fields.First(x => x.Name.Equals("Owner")).Value, out var owner);

            var admin = await _userManager.IsAdministrator(EmporiumUser.Create(EmporiumId, ReferenceNumber.Create(owner.RawValue)));

            if (!admin.IsSuccessful && owner.RawValue != Context.AuthorId)
                return ErrorResponse(embeds: responseEmbed.WithDescription("Unauthorized Access: Only the OWNER can reroll the Giveaway!"));

            var logs = await ParseLogContentAsync(logContent);
            var claimant = embed.Fields.First(x => x.Name.Equals("Claimed By")).Value;

            if (claimant.Equals("*REROLLED*"))
                return ErrorResponse(embeds: responseEmbed.WithDescription("Invalid Action: Giveaway has already been rerolled!"));

            var originalWinnerIds = Mention.ParseUsers(claimant).ToArray();
            var excludedWinnerIds = originalWinnerIds.ToArray();
            var participants = logs.Where(x => !originalWinnerIds.Any(userId => userId.Equals(x.User))).ToArray();

            if (participants.Length == 0 || participants.Length < originalWinnerIds.Length)
                return Response(responseEmbed.WithDescription("Not enough remaining participants to reroll..."));

            if (originalWinnerIds.Length > 1)
            {
                var result = await SelectRerollPositions(message, originalWinnerIds);

                if (result is null)
                {
                    await message.ModifyAsync(x => x.Components = Array.Empty<LocalRowComponent>());
                    return ErrorResponse(embeds: new LocalEmbed().WithDescription("Re-roll selection cancelled due to timeout"));
                }

                excludedWinnerIds = result.SelectedValues.Select(x => Snowflake.Parse(x)).ToArray();

                await result.ModifyMessageAsync(new LocalInteractionMessageResponse().WithComponents(Array.Empty<LocalRowComponent>()));
            }

            var modifiedEmbed = LocalEmbed.CreateFrom(embed);

            modifiedEmbed.Fields.Value.First(x => x.Name.Equals("Claimed By")).Value = "*REROLLED*";

            await message.ModifyAsync(x => x.Embeds = new[] { modifiedEmbed });

            var retainedWinnerIds = originalWinnerIds.Except(excludedWinnerIds).ToArray();
            var oldWinners = logs.Where(x => retainedWinnerIds.Any(userId => userId.Equals(x.User)));
            var newWinners = participants.OrderBy(x => _random.Next()).Take(excludedWinnerIds.Length);
            var index = modifiedEmbed.Description.Value.LastIndexOf("for") + 4;
            var winners = oldWinners.Concat(newWinners);
            var mentions = string.Join(" | ", winners.Select(x => Mention.User(x.User)));
            var description = modifiedEmbed.Description.Value[..index] + string.Join(", ", winners.Select(x => Markdown.Bold(x.Submission)));

            modifiedEmbed.Description = description;
            modifiedEmbed.Fields.Value.First(x => x.Name.Equals("Claimed By")).Value = mentions;

            var success = new LocalInteractionMessageResponse().WithContent("Giveaway successfully rerolled").WithIsEphemeral();
            var followup = new LocalInteractionMessageResponse().WithContent($"{Mention.User(owner)} | {mentions}").AddEmbed(modifiedEmbed);

            var stream = new MemoryStream();

            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync(logContent);
            await writer.FlushAsync();

            stream.Seek(0, SeekOrigin.Begin);

            await Context.Interaction.SendMessageAsync(success);
            await Context.Interaction.SendMessageAsync(followup.AddAttachment(new LocalAttachment(stream, $"{logs.Count} offers")).WithIsEphemeral(false));

            return Results.Success;
        }

        private async Task<ISelectionComponentInteraction> SelectRerollPositions(IUserMessage message, Snowflake[] originalWinnerIds)
        {
            await message.ModifyAsync(x =>
            {
                x.Components = new[]
                {
                    LocalComponent.Row(
                        LocalComponent.Selection($"reroll:{message.Id}",
                            originalWinnerIds.Select((x, index) =>
                            {
                                var place = index+1;
                                return new LocalSelectionComponentOption($"{place.ToOrdinalWords().Titleize()} Place", x.ToString());
                            }).ToArray()
                        ).WithPlaceholder("Select the positions to re-roll").WithMinimumSelectedOptions(1).WithMaximumSelectedOptions(originalWinnerIds.Length)
                    )
                };
            });

            await Context.Interaction.SendMessageAsync(
                new LocalInteractionMessageResponse()
                    .WithContent($"Select the positions to re-roll - {Discord.MessageJumpLink(Context.GuildId, Context.ChannelId, message.Id)}"));

            return await Context.WaitForInteractionAsync<ISelectionComponentInteraction>($"reroll:{message.Id}", x => x.AuthorId == Context.AuthorId, TimeSpan.FromSeconds(60));
        }

        [MessageCommand("Cancel Reschedule")]
        [Description("Cancel automatic re-listing the item once it's sold/expired")]
        public async Task<IResult> DisableAutoSchedule(IUserMessage message)
        {
            await Deferral(true);

            var responseEmbed = new LocalEmbed().WithDefaultColor();
            var response = ValidateListingMessage(message, responseEmbed);

            if (response != null) return response;

            var embed = message.Embeds[0];

            if (embed.Footer.IconUrl == null)
                return Response(responseEmbed.WithDescription("Item is not currently scheduled"));

            var result = await Base.ExecuteAsync(new UnscheduleListingCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(message.Id), embed.Title.Split(':')[0]));

            if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

            return Response(responseEmbed.WithDescription("Item will no longer be automatically relisted once sold/expired"));
        }

        [MessageCommand("View Logs")]
        [Description("View the logs attached to this message")]
        public async Task<IResult> ViewAttachedLog(IUserMessage message)
        {
            await Deferral(true);

            List<OfferLog> logs;
            IDiscordCommandResult commandResult;

            var responseEmbed = new LocalEmbed().WithDefaultColor();

            if (message.Attachments.Count == 0)
            {
                commandResult = ValidateListingMessage(message, responseEmbed);

                if (commandResult != null) return commandResult;

                var listing = await GetActiveListing(message);

                if (listing is null) return ErrorResponse(embeds: responseEmbed.WithDescription("Unable to view activity logs for this listing"));
                if (listing is VickreyAuction) return ErrorResponse(embeds: responseEmbed.WithDescription("Sealed bids cannot be revealed until the auction ends!"));

                var admin = await _userManager.IsAdministrator(listing.Owner);

                if (!admin.IsSuccessful && listing.Owner.ReferenceNumber.Value != Context.AuthorId)
                    return ErrorResponse(embeds: responseEmbed.WithDescription("Unauthorized Access: Only the OWNER can view the activity log!"));

                var offers = (listing.Product as AuctionItem)?.Offers.Select(x => new OfferLog(x.UserReference.Value, x.Amount.Value.ToString(), x.SubmittedOn)) 
                          ?? (listing.Product as GiveawayItem)?.Offers.Select(x => new OfferLog(x.UserReference.Value, x.Submission.Value, x.SubmittedOn));

                logs = offers.ToList();
            }
            else
            {
                commandResult = ValidateResultMessage(message, responseEmbed);

                if (commandResult != null) return commandResult;

                var attachment = message.Attachments[0];
                var logContent = await GetLogContentAsync(attachment.Url);

                if (logContent == null) return ErrorResponse(embeds: responseEmbed.WithDescription("Error: Unable to retrieve log file"));

                logs = await ParseLogContentAsync(logContent);
            }

            return logs.Count == 0 
                ? OkResponse(embeds: responseEmbed.WithDescription("No available logs exist")) 
                : SuccessResponse(embeds: ParseLogs(message.Embeds[0].Title, logs));
        }

        private async Task<Listing> GetActiveListing(IUserMessage message)
        {
            var embed = message.Embeds[0];
            var type = embed.GetListingType();
            var reference = ReferenceNumber.Create(message.Id);

            var query = type switch
            {
                { } when type.Contains("Auction", StringComparison.OrdinalIgnoreCase) => new GetListingDetailsQuery(EmporiumId, ShowroomId, "Auction") { ReferenceNumber = reference},
                { } when type.Contains("Giveaway", StringComparison.OrdinalIgnoreCase) => new GetListingDetailsQuery(EmporiumId, ShowroomId, "Giveaway") { ReferenceNumber = reference },
                _ => null
            };

            if (query is null) return null;

            var response = await Base.ExecuteAsync(query);

            if (response is null || response.Data is null) return null;

            return response.Data.Listing;
        }

        private async Task<string> GetLogContentAsync(string logUrl)
        {
            using var httpClient = _httpClientFactory.CreateClient("agora");
            var httpResponse = await httpClient.GetAsync(logUrl);

            if (httpResponse.IsSuccessStatusCode)
                return await httpResponse.Content.ReadAsStringAsync();

            return null;
        }

        private async Task<List<OfferLog>> ParseLogContentAsync(string logContent)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(logContent));
            return await JsonSerializer.DeserializeAsync<List<OfferLog>>(stream);
        }

        private static LocalEmbed[] ParseLogs(string title, List<OfferLog> logs)
        {
            IEnumerable<string> participants = GetParticipants(title, logs);
            List<LocalEmbed> embeds = CreateEmbeds(participants);

            return embeds.Take(5).ToArray();
        }

        private static IEnumerable<string> GetParticipants(string title, List<OfferLog> logs) => title switch
        {
            string x when x.Contains("Giveaway") => logs.OrderBy(x => x.SubmittedOn).Select(x => $"{Mention.User(x.User)}: {x.Submission}"),
            string x when x.Contains("Auction") => logs.OrderByDescending(x => double.Parse(x.Submission)).Select(x => $"{Mention.User(x.User)}: Bid {x.Submission}"),
            _ => Array.Empty<string>(),
        };

        private static List<LocalEmbed> CreateEmbeds(IEnumerable<string> participants)
        {
            List<LocalEmbed> embeds = new();
            StringBuilder description = new();

            foreach (var participant in participants)
            {
                if (description.Length + participant.Length > Discord.Limits.Message.Embed.MaxDescriptionLength)
                {
                    embeds.Add(new LocalEmbed().WithDefaultColor().WithDescription(description.ToString()));
                    description.Clear();
                }

                description.AppendLine(participant);
            }

            if (description.Length > 0)
                embeds.Add(new LocalEmbed().WithDefaultColor().WithDescription(description.ToString()));

            return embeds;
        }

        private DiscordInteractionResponseCommandResult ValidateListingMessage(IUserMessage message, LocalEmbed response)
        {
            response = response.WithDescription("Command can only be executed on item listing messages!");

            if (message.Embeds.Count != 1) return Response(response);
            if (message.Author.Id != Context.Bot.CurrentUser.Id) return Response(response);

            var embed = message.Embeds[0];

            if (embed.Fields.FirstOrDefault(x => x.Name.Equals("Item Owner") || x.Name.Equals("Requester")) == null) return Response(response);
            if (embed.Footer == null || !embed.Footer.Text.StartsWith("Reference Code:")) return Response(response);

            return null;
        }

        private DiscordInteractionResponseCommandResult ValidateResultMessage(IUserMessage message, LocalEmbed response)
        {
            response = response.WithDescription("Command can only be executed on a tranaction result messages!");

            if (message.Embeds.Count != 1) return Response(response);
            if (message.Author.Id != Context.Bot.CurrentUser.Id) return Response(response);

            var embed = message.Embeds[0];

            if (embed.Fields.FirstOrDefault(x => x.Name.Equals("Owner")) == null) return Response(response);
            if (embed.Fields.FirstOrDefault(x => x.Name.Equals("Claimed By")) == null) return Response(response);

            return null;
        }
    }
}
