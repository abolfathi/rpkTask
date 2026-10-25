using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public interface IOrderPersistence
{
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPrice>> GetProductPricesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);

    Task SaveOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task<Order?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchOrdersAsync(
        OrderSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<Order?> GetOrderForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<ConfirmOrderOutcome> ConfirmOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<bool> TryUpdateStatusAsync(
        Guid orderId,
        OrderStatus expectedCurrent,
        OrderStatus next,
        CancellationToken cancellationToken = default);
}
