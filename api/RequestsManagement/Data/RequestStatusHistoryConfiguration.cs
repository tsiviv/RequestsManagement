using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestsManagement.Models;

namespace RequestsManagement.Data;

public class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistoryEntity>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistoryEntity> builder)
    {
        builder.ToTable("RequestStatusHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.PreviousStatus)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(h => h.NewStatus)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(h => h.ChangedAt)
            .IsRequired();

        builder.Property(h => h.ChangedBy)
            .IsRequired()
            .HasMaxLength(RequestStatusHistoryEntity.ChangedByMaxLength);

        // Cascade so re-seeding (which deletes requests) also clears their history.
        builder.HasOne<RequestEntity>()
            .WithMany()
            .HasForeignKey(h => h.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Replaces the FK index EF would otherwise create on RequestId alone.
        builder.HasIndex(h => new { h.RequestId, h.ChangedAt })
            .HasDatabaseName("IX_RequestStatusHistory_RequestId_ChangedAt");
    }
}
