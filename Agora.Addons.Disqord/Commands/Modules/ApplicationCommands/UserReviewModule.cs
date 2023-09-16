using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Persistence.Models;
using Disqord;
using Disqord.Bot.Commands;
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
    public sealed class UserReviewModule : AgoraModuleBase
    {
        private readonly IUserProfileService _profileCache;

        public UserReviewModule(IUserProfileService userProfile)
        {
            _profileCache = userProfile;
        }

        [MessageCommand("Review Transaction")]
        [Description("Rate the owner after a successful transaction")]
        [RequireBotPermissions(Permissions.ReadMessageHistory)]
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

            if (participants is null) return Response(invalidMessage);
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
            
            if (participants is null) return Response(invalidMessage);
            if (participants.Count() != 2) return Response(invalidMessage);
            if (message.Embeds.Count != 1) return Response(invalidMessage);

            var owner = participants.First();
            var buyer = participants.Last();

            if (message.Embeds[0].Footer == null || !message.Embeds[0].Footer.Text.Equals("✅"))
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral().WithContent("This transaction has not been reviewed!"));

            if (Context.AuthorId != buyer)
                return Response(new LocalInteractionMessageResponse()
                        .WithIsEphemeral().WithContent($"Only {Mention.User(buyer)} can remove this review!"));

            var result = await Base.ExecuteAsync(new RemoveCommentCommand(EmporiumId, ReferenceNumber.Create(owner), Comment.Create(message.Id.ToString())));

            if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

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
            var profile = (UserProfile)await _profileCache.GetUserProfileAsync(member.GuildId, member.Id);

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
