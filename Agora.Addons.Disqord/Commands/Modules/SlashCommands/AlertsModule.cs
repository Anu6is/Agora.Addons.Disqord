using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Features.Commands;
using Agora.Shared.Persistence.Models;
using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Rest;
using Emporia.Extensions.Discord;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireBuyer]
    [SlashGroup("outbid")]
    public sealed class OutbidAlertModule : AgoraModuleBase
    {
        private readonly IUserProfileService _profileCache;

        public OutbidAlertModule(IUserProfileService userProfile)
        {
            _profileCache = userProfile;
        }

        [SlashCommand("alerts")]
        [Description("Enable/disable outbid notifications via Direct Messages")]
        public async Task<IResult> OutbidAlerts()
        {
            var profile = (UserProfile)await _profileCache.GetUserProfileAsync(Context.GuildId, Context.AuthorId);
            var enableAlerts = !profile.OutbidAlerts;

            if (enableAlerts && !await DirectMessagesEnabledAsync())
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral()
                        .AddEmbed(new LocalEmbed().WithDefaultColor()
                        .WithDescription($"Direct Messages must be enabled. Review your privacy settings for this server.")));

            profile.SetOutbidNotifications(enableAlerts);

            var result = await Base.ExecuteAsync(new UpdateUserProfileCommand(profile));

            if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral()
                    .AddEmbed(new LocalEmbed()
                        .WithColor(profile.OutbidAlerts ? Color.Teal : Color.Red)
                        .WithDescription($"Outbid notifications {Markdown.Bold(profile.OutbidAlerts ? "ENABLED" : "DISABLED")}")));
        }

        private async Task<bool> DirectMessagesEnabledAsync()
        {
            bool enabled = true;

            try
            {
                await Context.Author.SendMessageAsync(new LocalMessage().WithContent("Outbid notifications will be sent via Direct Messages"));
            }
            catch (Exception)
            {
                enabled = false;
            }
            return enabled;
        }
    }

    [RequireSetup]
    [RequireBuyer]
    //[SlashGroup("trade")]
    public sealed class TradeOfferAlertModule : AgoraModuleBase
    {
        private readonly IUserProfileService _profileCache;

        public TradeOfferAlertModule(IUserProfileService userProfile)
        {
            _profileCache = userProfile;
        }

        //[SlashCommand("alerts")]
        //[Description("Enable/disable trade deal notifications via Direct Messages")]
        public async Task<IResult> DealAlerts()
        {
            var profile = (UserProfile)await _profileCache.GetUserProfileAsync(Context.GuildId, Context.AuthorId);
            var enableAlerts = !profile.TradeDealAlerts;

            if (enableAlerts && !await DirectMessagesEnabledAsync())
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral()
                        .AddEmbed(new LocalEmbed().WithDefaultColor()
                        .WithDescription($"Direct Messages must be enabled. Review your privacy settings for this server.")));

            profile.SetTradeDealNotifications(enableAlerts);

            var result = await Base.ExecuteAsync(new UpdateUserProfileCommand(profile));

            if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral()
                    .AddEmbed(new LocalEmbed()
                        .WithColor(profile.TradeDealAlerts ? Color.Teal : Color.Red)
                        .WithDescription($"Trade notifications {Markdown.Bold(profile.TradeDealAlerts ? "ENABLED" : "DISABLED")}")));
        }

        private async Task<bool> DirectMessagesEnabledAsync()
        {
            bool enabled = true;

            try
            {
                await Context.Author.SendMessageAsync(new LocalMessage().WithContent("Trade notifications will be sent via Direct Messages"));
            }
            catch (Exception)
            {
                enabled = false;
            }
            return enabled;
        }
    }
}
