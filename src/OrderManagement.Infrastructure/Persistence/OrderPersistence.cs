using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;

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
}
