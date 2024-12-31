using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Extensions;
using Agora.Shared.Features.Commands;
using Agora.Shared.Persistence.Models;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Domain.Entities;
using Emporia.Domain.Events;
using Emporia.Extensions.Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Events;

internal class OfferAdded : INotificationHandler<OfferAddedNotification>
{
    private readonly DiscordBotBase _bot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserProfileService _userProfileService;

    public OfferAdded(DiscordBotBase bot, IUserProfileService profileService, IServiceScopeFactory scopeFactory)
    {
        _bot = bot;
        _scopeFactory = scopeFactory;
        _userProfileService = profileService;
    }

    public async Task Handle(OfferAddedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.Offer is not Deal tradeOffer) return;
        if (notification.Listing is not StandardTrade standardTrade || !standardTrade.AllowOffers) return;

        var owner = notification.Listing.Owner;
        var emporiumId = owner.EmporiumId.Value;
        var profile = (UserProfile)await _userProfileService.GetUserProfileAsync(emporiumId, owner.ReferenceNumber.Value);

        if (!profile.TradeDealAlerts) return;

        var trader = notification.Offer.UserReference.Value;
        var channelReference = notification.Listing.ReferenceCode.Reference();
        var channelId = channelReference == 0 ? notification.Listing.ShowroomId.Value : channelReference;
        var link = Discord.MessageJumpLink(emporiumId, channelId, standardTrade.Product.ReferenceNumber.Value);

        try
        {
            var listing = notification.Listing;
            var product = listing.Product.Title;
            var code = listing.ReferenceCode.Code();
            var offer = Markdown.Bold(tradeOffer.Submission.Value);

            var directChannel = await _bot.CreateDirectChannelAsync(owner.ReferenceNumber.Value, cancellationToken: cancellationToken);

            var embed = new LocalEmbed().WithTitle("[Counter Offer Submitted]")
                                        .WithDescription($"{Mention.User(trader)} is offering {offer} for [{listing}: {code}] {Markdown.Bold(product)}")
                                        .WithDefaultColor();

            if (!string.IsNullOrWhiteSpace(tradeOffer.Details)) embed.AddField("Additional Info", tradeOffer.Details);

            var showroomId = notification.Listing.ShowroomId.Value;
            var reference = notification.Listing.Product.ReferenceNumber.Value;
            var linkButton = LocalComponent.LinkButton(link, TranslateButton(owner.EmporiumId.Value, "View Item"));
            var rejectButton = LocalComponent.Button($"#reject:{emporiumId}:{showroomId}:{reference}:{trader}", TranslateButton(owner.EmporiumId.Value, "Reject Offer")).WithStyle(LocalButtonComponentStyle.Danger);
            var negotiateButton = LocalComponent.Button($"#negotiate:{emporiumId}:{showroomId}:{reference}:{trader}", TranslateButton(owner.EmporiumId.Value, "Negotiate Offer")).WithStyle(LocalButtonComponentStyle.Primary);
            var acceptButton = LocalComponent.Button($"#acknowledge:{emporiumId}:{showroomId}:{reference}:{trader}", TranslateButton(owner.EmporiumId.Value, "Accept Offer")).WithStyle(LocalButtonComponentStyle.Success);

            var row1 = LocalComponent.Row(linkButton, rejectButton, negotiateButton, acceptButton);

            await directChannel.SendMessageAsync(new LocalMessage().AddEmbed(embed).WithComponents(row1), cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new UpdateUserProfileCommand(profile.SetTradeDealNotifications(false)), cancellationToken);
        }
    }

    private string TranslateButton(ulong guildId, string key)
    {
        using var scope = _scopeFactory.CreateScope();
        var locale = _bot.GetGuild(guildId).PreferredLocale;
        var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
        localization.SetCulture(locale);

        return localization.Translate(key, "ButtonStrings");
    }
}
