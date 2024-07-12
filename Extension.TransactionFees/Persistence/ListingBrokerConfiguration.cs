using Emporia.Domain.Entities;
using Emporia.Persistence.Configuration;
using Extension.TransactionFees.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Extension.TransactionFees.Persistence;

public sealed class ListingBrokerConfiguration : EntityTypeConfiguration<ListingBroker>
{
    public override void Configure(EntityTypeBuilder<ListingBroker> builder, DatabaseFacade database)
    {
        builder.ToTable("ListingBroker");

        builder.HasOne<Listing>()
               .WithOne()
               .HasForeignKey<ListingBroker>(listing => listing.Id)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);
    }
}
