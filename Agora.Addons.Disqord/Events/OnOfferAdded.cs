using Emporia.Domain.Entities;
using Emporia.Domain.Events;
using Emporia.Extensions.Discord.Services;
using MediatR;

namespace Agora.Addons.Disqord.Events;

internal class OnOfferAdded(ListingExpirationJob expirationJob) : INotificationHandler<OfferAddedEvent>
{
    private readonly ListingExpirationJob _expirationJob = expirationJob;

    public Task Handle(OfferAddedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Listing is not LiveAuction auction) return Task.CompletedTask;

        notification.Listing.ExtendBy(auction.Timeout.Add(TimeSpan.FromSeconds(1)));
        
        _expirationJob.Schedule(notification.Listing);

        return Task.CompletedTask;
    }
}
