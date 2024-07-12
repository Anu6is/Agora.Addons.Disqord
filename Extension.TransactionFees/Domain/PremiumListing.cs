using Emporia.Domain.Common;

namespace Extension.TransactionFees.Domain;

public sealed class PremiumListing : Entity<ListingId>
{
    public ulong EntryRoleId { get; set; }
    public int RequiredEntries { get; set; }
    public List<ulong> EntryList { get; set; } = [];
    public TransactionFee EntryFee { get; set; }

    private PremiumListing(ListingId id) : base(id) { }

    public static PremiumListing Create(ListingId listingId)
    {
        return new PremiumListing(listingId);
    }
}
