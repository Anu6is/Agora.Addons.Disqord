using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Extensions;
using Agora.Shared.Features.Commands;
using Agora.Shared.Persistence.Models;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Emporia.Domain.Entities;
using Emporia.Domain.Events;
using Emporia.Extensions.Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Events
{
    internal class OfferRemoved : INotificationHandler<OfferRemovedNotification>
    {
        private readonly DiscordBotBase _bot;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IUserProfileService _userProfileService;

        public OfferRemoved(DiscordBotBase bot, IUserProfileService profileService, IServiceScopeFactory scopeFactory)
        {
            _bot = bot;
            _scopeFactory = scopeFactory;
            _userProfileService = profileService;
        }

        public async Task Handle(OfferRemovedNotification notification, CancellationToken cancellationToken)
        {
            if (notification.Offer is not Deal tradeOffer) return;
            if (notification.Listing is not StandardTrade standardTrade || !standardTrade.AllowOffers) return;

            var trader = notification.Offer.UserReference.Value;
            var owner = notification.Listing.Owner.ReferenceNumber.Value;
            var emporiumId = notification.Listing.Owner.EmporiumId.Value;
            var profile = (UserProfile)await _userProfileService.GetUserProfileAsync(emporiumId, trader);

            if (!profile.TradeDealAlerts) return;

            var channelReference = notification.Listing.ReferenceCode.Reference();
            var channelId = channelReference == 0 ? notification.Listing.ShowroomId.Value : channelReference;
            var link = Discord.MessageJumpLink(emporiumId, channelId, standardTrade.Product.ReferenceNumber.Value);

            try
            {
                var directChannel = await _bot.CreateDirectChannelAsync(trader, cancellationToken: cancellationToken);
                var embed = new LocalEmbed().WithTitle($"[Offer Rejected] {notification.Listing.Product.Title}").WithUrl(link)
                                            .WithDescription($"{Mention.User(owner)} rejected your offer of {Markdown.Bold(tradeOffer.Submission.Value)}")
                                            .WithDefaultColor();

                if (!string.IsNullOrWhiteSpace(tradeOffer.Details)) embed.AddField("Reason", tradeOffer.Details);

                await directChannel.SendMessageAsync(new LocalMessage().AddEmbed(embed), cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new UpdateUserProfileCommand(profile.SetTradeDealNotifications(false)), cancellationToken);
            }
        }
    }
}
