using Emporia.Domain.Common;

namespace Extension.TransactionFees.Domain;

public sealed class ListingBroker : Entity<ListingId>
{
    public ulong BrokerId { get; private set; }

    private ListingBroker(ListingId id) : base(id) { }

    public static ListingBroker Create(ListingId listingId, ulong brokerId)
    {
        return new ListingBroker(listingId) { BrokerId = brokerId };
    }
}
