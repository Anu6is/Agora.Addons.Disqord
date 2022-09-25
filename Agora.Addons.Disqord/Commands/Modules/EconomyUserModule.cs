using Agora.Addons.Disqord.Checks;
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
    public sealed class EconomyUserModule : AgoraModuleBase
    {
        private readonly IUserProfileService _profileCache;

        public EconomyUserModule(IUserProfileService userProfile)
        {
            _profileCache = userProfile;
        }

        [SlashCommand("alerts")]
        [Description("Enable/disable outbid notifications via Direct Messages")]
        public async Task<IResult> OutbidAlerts()
        {
            var profile = (UserProfile) await _profileCache.GetUserProfileAsync(Context.GuildId, Context.AuthorId);
            var enableAlerts = !profile.OutbidAlerts;

            if (enableAlerts && !await DirectMessagesEnabledAsync())
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral()
                        .AddEmbed(new LocalEmbed().WithDefaultColor()
                        .WithDescription($"Direct Messages must be enabled. Review your privacy settings for this server.")));

            profile.OutbidAlerts = enableAlerts;
            await Base.ExecuteAsync(new UpdateUserProfileCommand(profile));

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
}
