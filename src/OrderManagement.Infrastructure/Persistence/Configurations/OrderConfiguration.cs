using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .IsRequired();

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .IsRequired();

        builder.Property(o => o.IsDeleted)
            .IsRequired();

        builder.Property(o => o.DeletedAt);

        builder.HasQueryFilter(o => !o.IsDeleted);

        // Keep a dedicated CustomerId index for FK lookups (all rows, including soft-deleted).
        builder.HasIndex(o => o.CustomerId)
            .HasDatabaseName("IX_Orders_CustomerId");

        // Filtered indexes match the soft-delete query filter used by search/read queries.
        builder.HasIndex(o => new { o.CreatedAt, o.Id })
            .IsDescending(true, true)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Orders_CreatedAt_Id_Active");

        builder.HasIndex(o => new { o.CustomerId, o.Status, o.CreatedAt, o.Id })
            .IsDescending(false, false, true, true)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Orders_CustomerId_Status_CreatedAt_Id_Active");

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
