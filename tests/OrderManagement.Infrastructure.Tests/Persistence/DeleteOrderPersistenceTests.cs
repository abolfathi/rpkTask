using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Enums;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public sealed class DeleteOrderPersistenceTests
{
    private readonly SqlServerFixture _fixture;

    public DeleteOrderPersistenceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Pending_SoftDelete_Succeeds_WithoutInventoryChange()
    {
        await using var db = _fixture.CreateContext();
        var seeded = await SoftDeleteSeed.SeedOrderAsync(
            db, OrderStatus.Pending, orderQuantity: 3, inventoryQuantity: 10);
        var persistence = _fixture.CreatePersistence(db);

        var outcome = await persistence.DeleteOrderAsync(seeded.OrderId);

        Assert.Equal(DeleteOrderOutcomeKind.Success, outcome.Kind);

        await using var verify = _fixture.CreateContext();
        var order = await verify.Orders.IgnoreQueryFilters()
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == seeded.OrderId);
        var inventory = await verify.Inventories.SingleAsync(i => i.ProductId == seeded.ProductId);

        Assert.True(order.IsDeleted);
        Assert.NotNull(order.DeletedAt);
        Assert.Equal(10, inventory.Quantity);
        Assert.Single(order.Items);
        Assert.Null(await _fixture.CreatePersistence(verify).GetOrderByIdAsync(seeded.OrderId));
    }

    [Fact]
    public async Task Confirmed_SoftDelete_RestoresInventory()
    {
        await using var db = _fixture.CreateContext();
        // Inventory already reflects deduction after confirm (10 - 3 = 7).
        var seeded = await SoftDeleteSeed.SeedOrderAsync(
            db, OrderStatus.Confirmed, orderQuantity: 3, inventoryQuantity: 7);
        var persistence = _fixture.CreatePersistence(db);

        var outcome = await persistence.DeleteOrderAsync(seeded.OrderId);

        Assert.Equal(DeleteOrderOutcomeKind.Success, outcome.Kind);

        await using var verify = _fixture.CreateContext();
        var order = await verify.Orders.IgnoreQueryFilters()
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == seeded.OrderId);
        var inventory = await verify.Inventories.SingleAsync(i => i.ProductId == seeded.ProductId);

        Assert.True(order.IsDeleted);
        Assert.NotNull(order.DeletedAt);
        Assert.Equal(10, inventory.Quantity);
        Assert.Single(order.Items);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public async Task ShippedOrDelivered_Delete_ReturnsNotAllowed_NoSideEffects(OrderStatus status)
    {
        await using var db = _fixture.CreateContext();
        var seeded = await SoftDeleteSeed.SeedOrderAsync(
            db, status, orderQuantity: 2, inventoryQuantity: 5);
        var persistence = _fixture.CreatePersistence(db);

        var outcome = await persistence.DeleteOrderAsync(seeded.OrderId);

        Assert.Equal(DeleteOrderOutcomeKind.NotAllowed, outcome.Kind);

        await using var verify = _fixture.CreateContext();
        var order = await verify.Orders.SingleAsync(o => o.Id == seeded.OrderId);
        var inventory = await verify.Inventories.SingleAsync(i => i.ProductId == seeded.ProductId);

        Assert.False(order.IsDeleted);
        Assert.Null(order.DeletedAt);
        Assert.Equal(5, inventory.Quantity);
    }

    [Fact]
    public async Task SoftDeleted_ExcludedFromGetAndSearch()
    {
        await using var db = _fixture.CreateContext();
        var seeded = await SoftDeleteSeed.SeedOrderAsync(
            db, OrderStatus.Pending, orderQuantity: 1, inventoryQuantity: 4);
        var persistence = _fixture.CreatePersistence(db);

        await persistence.DeleteOrderAsync(seeded.OrderId);

        Assert.Null(await persistence.GetOrderByIdAsync(seeded.OrderId));

        var (items, total) = await persistence.SearchOrdersAsync(new OrderSearchCriteria
        {
            CustomerId = seeded.CustomerId,
            Page = 1,
            PageSize = 20
        });

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task SoftDeleted_RepeatDelete_ReturnsNotFound()
    {
        await using var db = _fixture.CreateContext();
        var seeded = await SoftDeleteSeed.SeedOrderAsync(
            db, OrderStatus.Pending, orderQuantity: 1, inventoryQuantity: 4);
        var persistence = _fixture.CreatePersistence(db);

        Assert.Equal(DeleteOrderOutcomeKind.Success, (await persistence.DeleteOrderAsync(seeded.OrderId)).Kind);
        var second = await persistence.DeleteOrderAsync(seeded.OrderId);

        Assert.Equal(DeleteOrderOutcomeKind.OrderNotFound, second.Kind);
    }

    [Fact]
    public async Task SoftDeleted_TryUpdateStatus_ReturnsFalse()
    {
        await using var db = _fixture.CreateContext();
        var seeded = await SoftDeleteSeed.SeedOrderAsync(
            db, OrderStatus.Pending, orderQuantity: 1, inventoryQuantity: 4);
        var persistence = _fixture.CreatePersistence(db);

        await persistence.DeleteOrderAsync(seeded.OrderId);

        var updated = await persistence.TryUpdateStatusAsync(
            seeded.OrderId, OrderStatus.Pending, OrderStatus.Confirmed);

        Assert.False(updated);
    }

    [Fact]
    public async Task Confirmed_Delete_WhenInventoryMissing_RollsBackSoftDelete()
    {
        await using var db = _fixture.CreateContext();
        var seeded = await SoftDeleteSeed.SeedOrderAsync(
            db,
            OrderStatus.Confirmed,
            orderQuantity: 2,
            inventoryQuantity: 0,
            createInventory: false);
        var persistence = _fixture.CreatePersistence(db);

        var outcome = await persistence.DeleteOrderAsync(seeded.OrderId);

        Assert.Equal(DeleteOrderOutcomeKind.ConcurrentConflict, outcome.Kind);

        await using var verify = _fixture.CreateContext();
        var order = await verify.Orders.IgnoreQueryFilters()
            .SingleAsync(o => o.Id == seeded.OrderId);

        Assert.False(order.IsDeleted);
        Assert.Null(order.DeletedAt);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }
}
