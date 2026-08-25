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

        // Supports "sort by last updated" — the only sortable column not already covered
        // by a composite index above. Without it, sorting by UpdatedAt has the same
        // unindexed-sort risk (tempdb spill under disk/AV contention) that Status/Priority/
        // CreatedAt are protected against.
        builder.HasIndex(r => r.UpdatedAt)
            .HasDatabaseName("IX_Requests_UpdatedAt");

        // Title/OrganizationName are intentionally not sortable (neither via the API's
        // SortableFields whitelist nor exposed as sortable columns in the UI) and so have
        // no dedicated index — only Status, Priority, CreatedAt, UpdatedAt are sortable.
    }
}
