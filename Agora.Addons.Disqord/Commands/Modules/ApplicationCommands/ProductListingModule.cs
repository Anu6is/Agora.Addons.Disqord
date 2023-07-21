using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Bot.Commands.Interaction;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
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

        public ProductListingModule(IHttpClientFactory clientFactory) => _httpClientFactory = clientFactory;

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

            await Base.ExecuteAsync(new UnscheduleListingCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(message.Id), embed.Title.Split(':')[0]));

            return Response(responseEmbed.WithDescription("Item will no longer be automatically relisted once sold/expired"));
        }

        [MessageCommand("View Logs")]
        [Description("View the logs attached to this message")]
        public async Task<IResult> ViewAttachedLog(IUserMessage message)
        {
            await Deferral(true);

            if (message.Attachments.Count == 0)
                return Response(new LocalEmbed().WithDefaultColor().WithDescription("There are no logs attached to this message"));

            var attachment = message.Attachments[0];
            var logContent = await GetLogContentAsync(attachment.Url);

            if (logContent == null)
                return Response(new LocalEmbed().WithDefaultColor().WithDescription("Error: Unable to retrieve log file"));

            var logs = await ParseLogContentAsync(logContent);

            return Response(ParseLogs(message.Embeds[0].Title, logs));
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
