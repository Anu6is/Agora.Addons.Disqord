using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Disqord;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireBuyer]
    public sealed class CreateOfferModule : AgoraModuleBase
    {
        [SlashCommand("bid")]
        [RequireBarterChannel]
        [Description("Submit a bid for an auction item.")]
        public async Task<IResult> AddBid([Description("The amount to bid on the listed item."), Minimum(0)] decimal amount)
        {
            var room = Channel as IThreadChannel;

            await Base.ExecuteAsync(new CreateBidCommand(EmporiumId, new ShowroomId(room.ChannelId.RawValue), ReferenceNumber.Create(room.Id.RawValue), amount));

            return Response(new LocalInteractionMessageResponse().WithContent("Bid Succesfully Submitted!").WithIsEphemeral());
        }
    }
}
