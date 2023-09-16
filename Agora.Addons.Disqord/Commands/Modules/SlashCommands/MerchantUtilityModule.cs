using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireMerchant]
    public sealed class MerchantUtilityModule : AgoraModuleBase
    {
        [RequireBarterChannel]
        [SlashCommand("Auto-Reschedule")]
        [Description("Automatically repost a listing once it ends")]
        public async Task<IResult> RescheduleListing([Description("When should the listing be rescheduled")] RescheduleOption when)
        {
            await Deferral(true);

            var responseEmbed = new LocalEmbed().WithDefaultColor();
            var product = Cache.GetCachedProduct(EmporiumId.Value, Context.ChannelId);

            if (when == RescheduleOption.Never)
            {
                var result = await Base.ExecuteAsync(new UnscheduleListingCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(product.ProductId), product.ListingType));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                return Response(responseEmbed.WithDescription("Item will no longer be automatically relisted once sold/expired"));
            }
            else
            {
                var result = await Base.ExecuteAsync(new ScheduleListingCommand(EmporiumId, ShowroomId, ReferenceNumber.Create(product.ProductId), product.ListingType, when));

                if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

                return Response(responseEmbed.WithDescription($"Item will be automatically relisted {(when == RescheduleOption.Always ? "" : $"once {when}")}"));
            }
        }
    }
}
