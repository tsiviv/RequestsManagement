using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestsManagement.Models;

namespace RequestsManagement.Data;

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

        builder.Property(r => r.AssignedTo)
            .IsRequired()
            .HasMaxLength(100);

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

        // Every sortable order (RequestService.ApplySort) matches one of these keys exactly; a
        // non-clustered key implicitly ends with the clustered Id, which is the order's tiebreaker.

        // Supports "filter by status, newest first" (seek on Status, CreatedAt already in order)
        // and the "requests by status" aggregation. Status is filtered on, not sorted by.
        builder.HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("IX_Requests_Status_CreatedAt");

        // Same for priority: filter + newest first, and the "requests by priority" aggregation.
        builder.HasIndex(r => new { r.Priority, r.CreatedAt })
            .HasDatabaseName("IX_Requests_Priority_CreatedAt");

        // Supports "a handler's queue, newest first", handler + date-range filters, and sorting by
        // handler (AssignedTo, CreatedAt, Id): one index for the filter, the range and the sort.
        builder.HasIndex(r => new { r.AssignedTo, r.CreatedAt })
            .HasDatabaseName("IX_Requests_AssignedTo_CreatedAt");

        // Same shape for the exact OrganizationName filter and sorting by organization. Free-text
        // search (LIKE '%term%') cannot seek on this index; see docs for that trade-off.
        builder.HasIndex(r => new { r.OrganizationName, r.CreatedAt })
            .HasDatabaseName("IX_Requests_OrganizationName_CreatedAt");

        // Supports the unfiltered default listing (newest first), CreatedAt range filters and
        // date-based aggregation.
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Requests_CreatedAt");

        // Supports sorting by last update ("not touched recently" / "just changed"). Without it,
        // that sort would be a Sort over the whole table on every page.
        builder.HasIndex(r => r.UpdatedAt)
            .HasDatabaseName("IX_Requests_UpdatedAt");

        // Sorting by Id reads the clustered index (PK) in order. Title is not sortable: it would
        // need its own index for a free-text column whose order means little to users.
    }
}
