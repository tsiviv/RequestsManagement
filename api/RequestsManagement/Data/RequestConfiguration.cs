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

        // Equality filter first, then CreatedAt (range and sort). Non-clustered keys end with Id,
        // so every sort order (RequestQueryService.ApplySort) is read in index order without a Sort.

        builder.HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("IX_Requests_Status_CreatedAt");

        builder.HasIndex(r => new { r.Priority, r.CreatedAt })
            .HasDatabaseName("IX_Requests_Priority_CreatedAt");

        builder.HasIndex(r => new { r.AssignedTo, r.CreatedAt })
            .HasDatabaseName("IX_Requests_AssignedTo_CreatedAt");

        builder.HasIndex(r => new { r.OrganizationName, r.CreatedAt })
            .HasDatabaseName("IX_Requests_OrganizationName_CreatedAt");

        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Requests_CreatedAt");

        builder.HasIndex(r => r.UpdatedAt)
            .HasDatabaseName("IX_Requests_UpdatedAt");

    }
}
