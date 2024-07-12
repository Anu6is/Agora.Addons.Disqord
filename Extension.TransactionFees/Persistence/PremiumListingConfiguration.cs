using Emporia.Domain.Entities;
using Emporia.Persistence.Configuration;
using Extension.TransactionFees.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Extension.TransactionFees.Persistence;

public sealed class PremiumListingConfiguration : EntityTypeConfiguration<PremiumListing>
{
    public override void Configure(EntityTypeBuilder<PremiumListing> builder, DatabaseFacade database)
    {
        var options = new JsonSerializerOptions();

        builder.ToTable("PremiumListings");

        builder.Property(p => p.EntryList).IsRequired()
               .HasConversion(list => JsonSerializer.Serialize<List<ulong>>(list, options),
                              json => JsonSerializer.Deserialize<List<ulong>>(json, options)!,
                                      new ValueComparer<List<ulong>>(
                                          (c1, c2) => new HashSet<ulong>(c1!).SetEquals(new HashSet<ulong>(c2!)),
                                          c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                                          c => c.ToList()
                                          ));

        builder.HasOne<Listing>()
               .WithOne()
               .HasForeignKey<PremiumListing>(listing => listing.Id)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);
    }
}
