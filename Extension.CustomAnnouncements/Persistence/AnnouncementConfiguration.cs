using Emporia.Domain.Entities;
using Emporia.Persistence.Configuration;
using Extension.CustomAnnouncements.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Extension.CustomAnnouncements.Persistence;

public sealed class AnnouncementConfiguration : EntityTypeConfiguration<Announcement>
{
    public override void Configure(EntityTypeBuilder<Announcement> builder, DatabaseFacade database)
    {
        builder.ToTable("Announcements");

        builder.HasOne<Emporium>()
               .WithMany()
               .HasForeignKey(x => x.EmporiumId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.GuildId, x.AnnouncementType })
               .IsUnique();
    }
}
