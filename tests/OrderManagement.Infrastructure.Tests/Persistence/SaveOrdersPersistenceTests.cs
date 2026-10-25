using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public sealed class SaveOrdersPersistenceTests
{
    private readonly SqlServerFixture _fixture;

    public SaveOrdersPersistenceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveOrdersAsync_PersistsMultiplePendingOrders_WithoutInventoryChange()
    {
        await using var db = _fixture.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        const int inventoryBefore = 20;

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Bulk Customer",
            Email = $"{customerId:N}@example.com"
        });
        db.Products.Add(new Product
        {
            Id = productId,
            Name = "Bulk Product",
            Description = "Seed",
            Price = 8m,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = inventoryBefore,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();
        var orders = new List<Order>
        {
            new()
            {
                Id = order1Id,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                TotalAmount = 16m,
                CreatedAt = now,
                UpdatedAt = now,
                Items =
                [
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order1Id,
                        ProductId = productId,
                        Quantity = 2,
                        UnitPrice = 8m
                    }
                ]
            },
            new()
            {
                Id = order2Id,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                TotalAmount = 24m,
                CreatedAt = now,
                UpdatedAt = now,
                Items =
                [
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order2Id,
                        ProductId = productId,
                        Quantity = 3,
                        UnitPrice = 8m
                    }
                ]
            }
        };

        var persistence = _fixture.CreatePersistence(db);
        await persistence.SaveOrdersAsync(orders);

        await using var verify = _fixture.CreateContext();
        var saved = await verify.Orders
            .Include(o => o.Items)
            .Where(o => o.Id == order1Id || o.Id == order2Id)
            .ToListAsync();
        var inventory = await verify.Inventories.SingleAsync(i => i.ProductId == productId);

        Assert.Equal(2, saved.Count);
        Assert.All(saved, o => Assert.Equal(OrderStatus.Pending, o.Status));
        Assert.All(saved, o => Assert.False(o.IsDeleted));
        Assert.Equal(2, saved.Sum(o => o.Items.Count));
        Assert.Equal(inventoryBefore, inventory.Quantity);
    }

    [Fact]
    public async Task SaveOrdersAsync_OnItemFkFailure_RollsBackOrders()
    {
        await using var db = _fixture.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var customerId = Guid.NewGuid();
        var validProductId = Guid.NewGuid();
        var missingProductId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Rollback Customer",
            Email = $"{customerId:N}@example.com"
        });
        db.Products.Add(new Product
        {
            Id = validProductId,
            Name = "Valid Product",
            Description = "Seed",
            Price = 5m,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var goodOrderId = Guid.NewGuid();
        var badOrderId = Guid.NewGuid();
        var orders = new List<Order>
        {
            new()
            {
                Id = goodOrderId,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                TotalAmount = 5m,
                CreatedAt = now,
                UpdatedAt = now,
                Items =
                [
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = goodOrderId,
                        ProductId = validProductId,
                        Quantity = 1,
                        UnitPrice = 5m
                    }
                ]
            },
            new()
            {
                Id = badOrderId,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                TotalAmount = 5m,
                CreatedAt = now,
                UpdatedAt = now,
                Items =
                [
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = badOrderId,
                        ProductId = missingProductId,
                        Quantity = 1,
                        UnitPrice = 5m
                    }
                ]
            }
        };

        var persistence = _fixture.CreatePersistence(db);
        await Assert.ThrowsAnyAsync<Exception>(() => persistence.SaveOrdersAsync(orders));

        await using var verify = _fixture.CreateContext();
        var leftover = await verify.Orders
            .IgnoreQueryFilters()
            .Where(o => o.Id == goodOrderId || o.Id == badOrderId)
            .ToListAsync();
        var leftoverItems = await verify.OrderItems
            .Where(i => i.OrderId == goodOrderId || i.OrderId == badOrderId)
            .ToListAsync();

        Assert.Empty(leftover);
        Assert.Empty(leftoverItems);
    }
}
