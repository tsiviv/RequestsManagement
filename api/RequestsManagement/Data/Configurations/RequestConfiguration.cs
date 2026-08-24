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

        // Supports "filter by priority, sorted by newest" (Priority leads so it can be
        // seeked, CreatedAt trails so the seek is already in sort order); Priority still
        // leads for the "requests by priority" aggregation.
        builder.HasIndex(r => new { r.Priority, r.CreatedAt })
            .HasDatabaseName("IX_Requests_Priority_CreatedAt");

        // Supports the unfiltered default-sort listing and date-based aggregation.
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Requests_CreatedAt");

        // Supports "sort by title/organization" (both are sortable columns). Without these,
        // SQL Server has to Sort the whole table for every page — cheap in CPU (~50-90ms for
        // 12k rows) but the sort can spill to tempdb, which under real disk/AV contention
        // occasionally stalls for 20+ seconds. An index lets it do an ordered index scan
        // instead, with no sort and no spill risk.
        builder.HasIndex(r => r.Title)
            .HasDatabaseName("IX_Requests_Title");

        builder.HasIndex(r => r.OrganizationName)
            .HasDatabaseName("IX_Requests_OrganizationName");
    }
}
