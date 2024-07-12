using Emporia.Domain.Entities;
using Emporia.Persistence.Configuration;
using Extension.TransactionFees.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Extension.TransactionFees.Persistence;

public sealed class TransactionFeeSettingsConfiguration : EntityTypeConfiguration<TransactionFeeSettings>
{
    public override void Configure(EntityTypeBuilder<TransactionFeeSettings> builder, DatabaseFacade database)
    {
        builder.ToTable("TransactionFeeSettings");

        builder.HasOne<Emporium>()
               .WithOne()
               .HasForeignKey<TransactionFeeSettings>(settings => settings.Id)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);
    }
}
