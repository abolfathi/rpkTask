using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure.Tests.Persistence;

internal static class SoftDeleteSeed
{
    public sealed record SeededOrder(
        Guid OrderId,
        Guid ProductId,
        Guid CustomerId,
        int OrderQuantity,
        int InventoryQuantity);

    public static async Task<SeededOrder> SeedOrderAsync(
        AppDbContext db,
        OrderStatus status,
        int orderQuantity,
        int inventoryQuantity,
        bool createInventory = true)
    {
        var now = DateTimeOffset.UtcNow;
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Test Customer",
            Email = $"{customerId:N}@example.com"
        });

        db.Products.Add(new Product
        {
            Id = productId,
            Name = "Test Product",
            Description = "Seed",
            Price = 10m,
            CreatedAt = now,
            UpdatedAt = now
        });

        if (createInventory)
        {
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Quantity = inventoryQuantity,
                UpdatedAt = now
            });
        }

        db.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            Status = status,
            TotalAmount = 10m * orderQuantity,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = productId,
                    Quantity = orderQuantity,
                    UnitPrice = 10m
                }
            ]
        });

        await db.SaveChangesAsync();
        return new SeededOrder(orderId, productId, customerId, orderQuantity, inventoryQuantity);
    }
}
