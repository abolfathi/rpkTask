using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Infrastructure.Persistence;

public sealed class OrderPersistence : IOrderPersistence
{
    private readonly AppDbContext _dbContext;

    public OrderPersistence(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductPrice>> GetProductPricesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductPrice(p.Id, p.Price))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchOrdersAsync(
        OrderSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders.AsNoTracking().AsQueryable();

        if (criteria.CustomerId is { } customerId)
        {
            query = query.Where(o => o.CustomerId == customerId);
        }

        if (criteria.Status is { } status)
        {
            query = query.Where(o => o.Status == status);
        }

        if (criteria.CreatedFrom is { } createdFrom)
        {
            query = query.Where(o => o.CreatedAt >= createdFrom);
        }

        if (criteria.CreatedTo is { } createdTo)
        {
            query = query.Where(o => o.CreatedAt <= createdTo);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageIds = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        if (pageIds.Count == 0)
        {
            return (Array.Empty<Order>(), totalCount);
        }

        var items = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => pageIds.Contains(o.Id))
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Order?> GetOrderForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
    }

    public async Task<ConfirmOrderOutcome> ConfirmOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ConfirmOrderOutcome.OrderNotFound();
            }

            if (order.Status != OrderStatus.Pending)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ConfirmOrderOutcome.InvalidState(
                    $"Order is '{order.Status}' and cannot be confirmed.");
            }

            // Stable ProductId order so concurrent multi-product confirms acquire inventory locks consistently.
            var requiredByProduct = order.Items
                .GroupBy(i => i.ProductId)
                .OrderBy(g => g.Key)
                .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
                .ToList();

            var productIds = requiredByProduct.Select(r => r.ProductId).ToList();
            var inventories = await _dbContext.Inventories
                .Where(i => productIds.Contains(i.ProductId))
                .ToListAsync(cancellationToken);

            var inventoryByProduct = inventories.ToDictionary(i => i.ProductId);

            foreach (var (productId, requiredQty) in requiredByProduct)
            {
                if (!inventoryByProduct.TryGetValue(productId, out var inventory))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ConfirmOrderOutcome.InsufficientStock(
                        $"No inventory record found for product '{productId}'.");
                }

                if (inventory.Quantity < requiredQty)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ConfirmOrderOutcome.InsufficientStock(
                        $"Insufficient inventory for product '{productId}'. Required {requiredQty}, available {inventory.Quantity}.");
                }
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var (productId, requiredQty) in requiredByProduct)
            {
                var affected = await _dbContext.Inventories
                    .Where(i => i.ProductId == productId && i.Quantity >= requiredQty)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(i => i.Quantity, i => i.Quantity - requiredQty)
                            .SetProperty(i => i.UpdatedAt, now),
                        cancellationToken);

                if (affected != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ConfirmOrderOutcome.ConcurrentConflict(
                        $"Inventory for product '{productId}' changed during confirmation. Try again.");
                }
            }

            var orderAffected = await _dbContext.Orders
                .Where(o => o.Id == orderId && o.Status == OrderStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(o => o.Status, OrderStatus.Confirmed)
                        .SetProperty(o => o.UpdatedAt, now),
                    cancellationToken);

            if (orderAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ConfirmOrderOutcome.ConcurrentConflict(
                    "The order was modified by another request during confirmation.");
            }

            await transaction.CommitAsync(cancellationToken);

            // Detach tracked entities so a fresh read reflects ExecuteUpdate results.
            _dbContext.ChangeTracker.Clear();

            var confirmed = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == orderId, cancellationToken);

            return ConfirmOrderOutcome.Success(confirmed);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> TryUpdateStatusAsync(
        Guid orderId,
        OrderStatus expectedCurrent,
        OrderStatus next,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await _dbContext.Orders
            .Where(o => o.Id == orderId && o.Status == expectedCurrent)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.Status, next)
                    .SetProperty(o => o.UpdatedAt, now),
                cancellationToken);

        return affected == 1;
    }

    public async Task<DeleteOrderOutcome> DeleteOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeleteOrderOutcome.OrderNotFound();
            }

            if (order.Status != OrderStatus.Pending)
            {
                var requiredByProduct = order.Items
                    .GroupBy(i => i.ProductId)
                    .OrderBy(g => g.Key)
                    .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
                    .ToList();

                var now = DateTimeOffset.UtcNow;
                foreach (var (productId, restoreQty) in requiredByProduct)
                {
                    var affected = await _dbContext.Inventories
                        .Where(i => i.ProductId == productId)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(i => i.Quantity, i => i.Quantity + restoreQty)
                                .SetProperty(i => i.UpdatedAt, now),
                            cancellationToken);

                    if (affected != 1)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return DeleteOrderOutcome.ConcurrentConflict(
                            $"Inventory for product '{productId}' could not be restored during delete. Try again.");
                    }
                }
            }

            _dbContext.Orders.Remove(order);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return DeleteOrderOutcome.Success();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
