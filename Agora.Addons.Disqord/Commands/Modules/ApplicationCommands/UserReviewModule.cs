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
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands;

[RequireSetup]
[RequireBuyer]
public sealed class UserReviewModule : AgoraModuleBase
{
    private readonly IUserProfileService _profileCache;
    private readonly IServiceScopeFactory _scopeFactory;

    public UserReviewModule(IUserProfileService userProfile, IServiceScopeFactory scopeFactory)
    {
        _profileCache = userProfile;
        _scopeFactory = scopeFactory;
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
                                                                              TranslateButton("Submit Bot Review"))))
                                    .WithIsEphemeral();

        if (message.Author.Id != Context.Bot.CurrentUser.Id) return Response(invalidMessage);

        if (message.Embeds is not { Count: 1 }) return Response(invalidMessage);

        var embed = message.Embeds[0];
        var ownerField = embed.Fields.FirstOrDefault(field => field.Name.Equals("Owner"));
        var buyerField = embed.Fields.FirstOrDefault(field => field.Name.Equals("Claimed By"));

        if (ownerField is null || buyerField is null) return Response(invalidMessage);

        var participants = Mention.ParseUsers($"{ownerField.Value} {buyerField.Value}");
        var owner = participants.First();
        var claimaints = participants.Skip(1);

        if (owner.Equals(Context.AuthorId))
            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral().WithContent("You cannot review yourself!"));

        if (claimaints.Count() > 1)
            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral().WithContent("Only items with a single claimant can be reviewed as only 1 review can be submitted!"));

        var winner = claimaints.First();

        if (winner != Context.AuthorId)
            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral().WithContent($"Only {Mention.User(winner)} can review this transaction!"));

        if (embed.Footer != null && embed.Footer.Text.Equals("✅"))
            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral().WithContent("This transaction has already been reviewed!"));

        return Response(new LocalInteractionMessageResponse()
                .WithIsEphemeral()
                .AddEmbed(new LocalEmbed().WithDefaultColor().WithDescription($"How do you rate the service from {Mention.User(owner)}"))
                .AddComponent(LocalComponent.Row(LocalComponent.Selection($"rate-owner:{owner}:{Context.AuthorId}:{message.Id}", RatingSelectionOptions()))));
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

        if (message.Embeds is not { Count: 1 }) return Response(invalidMessage);

        var embed = message.Embeds[0];
        var ownerField = embed.Fields.FirstOrDefault(field => field.Name.Equals("Owner"));
        var buyerField = embed.Fields.FirstOrDefault(field => field.Name.Equals("Claimed By"));

        if (ownerField is null || buyerField is null) return Response(invalidMessage);

        var participants = Mention.ParseUsers($"{ownerField.Value} {buyerField.Value}");
        var owner = participants.First();
        var buyer = participants.Last();

        if (embed.Footer == null || !embed.Footer.Text.Equals("✅"))
            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral().WithContent("This transaction has not been reviewed!"));

        if (Context.AuthorId != buyer)
            return Response(new LocalInteractionMessageResponse()
                    .WithIsEphemeral().WithContent($"Only {Mention.User(buyer)} can remove this review!"));

        var result = await Base.ExecuteAsync(new RemoveCommentCommand(EmporiumId, ReferenceNumber.Create(owner), Comment.Create(message.Id.ToString())));

        if (!result.IsSuccessful) return ErrorResponse(isEphimeral: true, content: result.FailureReason);

        var updatedEmbed = LocalEmbed.CreateFrom(embed).WithFooter("review this transaction | right-click -> apps -> review");

        await message.ModifyAsync(x => x.Embeds = new[] { updatedEmbed });

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

    private string TranslateButton(string key)
    {
        using var scope = _scopeFactory.CreateScope();
        var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
        localization.SetCulture(Context.GuildLocale);

        return localization.Translate(key, "ButtonStrings");
    }
}
