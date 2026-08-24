using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestsManagement.Models;

namespace RequestsManagement.Data.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<RequestEntity>
{
    public void Configure(EntityTypeBuilder<RequestEntity> builder)
    {
        builder.ToTable("Requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.OrganizationName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired();

        builder.Property(r => r.RowVersion)
            .IsRowVersion();

        // Supports "filter by status, sorted by newest" — the most common list query.
        builder.HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("IX_Requests_Status_CreatedAt");

        // Supports priority filtering and the "requests by priority" aggregation.
        builder.HasIndex(r => r.Priority)
            .HasDatabaseName("IX_Requests_Priority");

        // Supports the unfiltered default-sort listing and date-based aggregation.
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Requests_CreatedAt");
    }
}
