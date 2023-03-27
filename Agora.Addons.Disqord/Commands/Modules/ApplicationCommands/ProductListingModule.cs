using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Bot.Commands.Interaction;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    public sealed class ProductListingModule : AgoraModuleBase
    {
        [MessageCommand("Auto-Reschedule")]
        [Description("Automatically re-list the item once it's sold/expired")]
        public async Task<IResult> EnableAutoSchedule(IUserMessage message)
        {
            await Deferral(true);

            var responseEmbed = new LocalEmbed().WithDefaultColor();
            var response = ValidateMessage(message, responseEmbed);

            if (response != null) return response;

            var embed = message.Embeds[0];

            if (embed.Footer.IconUrl != null)
                return Response(responseEmbed.WithDescription("Item is already scheduled to be relisted"));

            await Base.ExecuteAsync(new ScheduleListingCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(message.Id), embed.Title.Split(':')[0]));

            return Response(responseEmbed.WithDescription("Item will be automatically relisted once sold/expired"));
        }

        [MessageCommand("Cancel Reschedule")]
        [Description("Cancel automatic re-listing the item once it's sold/expired")]
        public async Task<IResult> DisableAutoSchedule(IUserMessage message)
        {
            await Deferral(true);

            var responseEmbed = new LocalEmbed().WithDefaultColor();
            var response = ValidateMessage(message, responseEmbed);

            if (response != null) return response;

            var embed = message.Embeds[0];

            if (embed.Footer.IconUrl == null)
                return Response(responseEmbed.WithDescription("Item is not currently scheduled"));

            await Base.ExecuteAsync(new UnscheduleListingCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(message.Id), embed.Title.Split(':')[0]));

            return Response(responseEmbed.WithDescription("Item will no longer be automatically relisted once sold/expired"));
        }

        private DiscordInteractionResponseCommandResult ValidateMessage(IUserMessage message, LocalEmbed response)
        {
            response = response.WithDescription("Command can only be executed on item listing messages!");

            if (message.Embeds.Count != 1) return Response(response);
            if (message.Author.Id != Context.Bot.CurrentUser.Id) return Response(response);

            var embed = message.Embeds[0];

            if (embed.Fields.FirstOrDefault(x => x.Name.Equals("Item Owner")) == null) return Response(response);
            if (embed.Footer == null || !embed.Footer.Text.StartsWith("Reference Code:")) return Response(response);

            return null;
        }
    }
}
