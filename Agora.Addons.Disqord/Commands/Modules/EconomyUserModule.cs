using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Features.Commands;
using Agora.Shared.Persistence.Models;
using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireBuyer]
    [SlashGroup("outbid")]
    public sealed class EconomyUserBidAlertModule : AgoraModuleBase
    {
        private readonly IUserProfileService _profileCache;

        public EconomyUserBidAlertModule(IUserProfileService userProfile)
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

    [RequireSetup]
    [RequireBuyer]
    [SlashGroup("trade")]
    public sealed class EconomyUserTradeAlertModule : AgoraModuleBase
    {
        private readonly IUserProfileService _profileCache;

        public EconomyUserTradeAlertModule(IUserProfileService userProfile)
        {
            _profileCache = userProfile;
        }

        [SlashCommand("alerts")]
        [Description("Enable/disable trade deal notifications via Direct Messages")]
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

            await Base.ExecuteAsync(new UpdateUserProfileCommand(profile));

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

    [RequireSetup]
    [RequireBuyer]
    public sealed class EconomyUserModule : AgoraModuleBase
    {
        private readonly IUserProfileService _profileCache;

        public EconomyUserModule(IUserProfileService userProfile)
        {
            _profileCache = userProfile;
        }

        [MessageCommand("Review Transaction")]
        [Description("Rate the owner after a successful transaction")]
        public IResult ReviewTransaction(IUserMessage message)
        {
            var invalidMessage = new LocalInteractionMessageResponse()
                                        .AddEmbed(new LocalEmbed()
                                                        .WithDescription("Only successful transaction messages can be reviewed!")
                                                        .WithFooter("Maybe you want to review the bot instead?")
                                                        .WithDefaultColor())
                                        .AddComponent(LocalComponent.Row(
                                                        LocalComponent.LinkButton("https://top.gg/bot/372831574942679040",
                                                                                  "Submit Bot Review")))
                                        .WithIsEphemeral();

            if (message.Author.Id != Context.Bot.CurrentUser.Id) return Response(invalidMessage);

            var participants = Mention.ParseUsers(message.Content.Replace("|", ""));

            if (participants.Count() != 2) return Response(invalidMessage);
            if (message.Embeds.Count != 1) return Response(invalidMessage);

            var owner = participants.First();
            var buyer = participants.Last();

            if (owner.Equals(Context.AuthorId))
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral().WithContent("You cannot review yourself!"));
            
            if (Context.AuthorId != buyer)
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral().WithContent($"Only {Mention.User(buyer)} can review this transaction!"));

            if (message.Embeds[0].Footer != null && message.Embeds[0].Footer.Text.Equals("✅"))
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral().WithContent("This transaction has already been reviewed!"));

            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral()
                    .AddEmbed(new LocalEmbed().WithDefaultColor().WithDescription($"How do you rate the service from {Mention.User(owner)}"))
                    .AddComponent(LocalComponent.Row(LocalComponent.Selection($"rate-owner:{owner}:{buyer}:{message.Id}", RatingSelectionOptions()))));
        }

        [MessageCommand("Remove Review")]
        [Description("Retract a previously submitted transaction review")]
        public async Task<IResult> RetractReview(IUserMessage message)
        {
            var invalidMessage = new LocalInteractionMessageResponse()
                                        .AddEmbed(new LocalEmbed()
                                                        .WithDescription("This is not a valid completed transaction message")
                                                        .WithDefaultColor())
                                        .WithIsEphemeral();

            if (message.Author.Id != Context.Bot.CurrentUser.Id) return Response(invalidMessage);
            
            var participants = Mention.ParseUsers(message.Content.Replace("|", ""));
            var owner = participants.First();
            var buyer = participants.Last();

            if (participants.Count() != 2) return Response(invalidMessage);

            if (message.Embeds[0].Footer == null || !message.Embeds[0].Footer.Text.Equals("✅"))
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral().WithContent("This transaction has not been reviewed!"));

            if (Context.AuthorId != buyer)
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral().WithContent($"Only {Mention.User(buyer)} can remove this review!"));

            await Base.ExecuteAsync(new RemoveCommentCommand(EmporiumId, ReferenceNumber.Create(owner), Comment.Create(message.Id.ToString())));

            var embed = LocalEmbed.CreateFrom(message.Embeds[0]).WithFooter("review this transaction | right-click -> apps -> review");
            
            await message.ModifyAsync(x => x.Embeds = new[] { embed });

            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral().WithContent("Review removed"));
        }

        [UserCommand("Merchant Rating")]
        [Description("View the rating score of this user. 5 Star Rating System")]
        public async Task<IResult> MerchantRating(IMember member)
        {
            if (member.IsBot) return Results.Success;

            var message = new LocalInteractionMessageResponse().WithIsEphemeral();
            var profile = (UserProfile) await _profileCache.GetUserProfileAsync(member.GuildId, member.Id);

            var color = profile.Rating switch
            {
                1 => Color.Red,
                2 => Color.Orange,
                5 => Color.Teal,
                _ => Color.LightGreen
            };

            if (profile.Reviews < 10)
                return Response(message.AddEmbed(new LocalEmbed().WithDefaultColor()
                    .WithAuthor(member)
                    .WithDescription("Unrated Merchant")
                    .WithFooter($"Total Reviews: {profile.Reviews}")));
            else
                return Response(message.AddEmbed(new LocalEmbed().WithColor(color)
                    .WithAuthor(member)
                    .WithDescription($"Merchant Rating: {profile.Rating}")
                    .WithFooter($"Total Reviews: {profile.Reviews}")));
        }

        private static LocalSelectionComponentOption[] RatingSelectionOptions()
        {
            var ratings = new[]
            {
                new LocalSelectionComponentOption("Avoid", "1").WithEmoji(LocalEmoji.Unicode("😠")),
                new LocalSelectionComponentOption("Poor", "2").WithEmoji(LocalEmoji.Unicode("😞")),
                new LocalSelectionComponentOption("OK", "3").WithEmoji(LocalEmoji.Unicode("🙂")),
                new LocalSelectionComponentOption("Good", "4").WithEmoji(LocalEmoji.Unicode("😀")),
                new LocalSelectionComponentOption("Recommend", "5").WithEmoji(LocalEmoji.Unicode("😆"))
            };

            return ratings;
        }
    }
}
