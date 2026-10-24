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
}
