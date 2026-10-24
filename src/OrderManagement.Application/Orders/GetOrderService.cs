namespace OrderManagement.Application.Orders;

public sealed class GetOrderService
{
    private readonly IOrderPersistence _persistence;

    public GetOrderService(IOrderPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<GetOrderResult> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _persistence.GetOrderByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return GetOrderResult.NotFound($"Order '{orderId}' was not found.");
        }

        return GetOrderResult.Success(OrderResponseMapper.ToResponse(order));
    }
}
