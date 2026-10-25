namespace OrderManagement.Application.Orders;

public sealed class DeleteOrderService
{
    private readonly IOrderPersistence _persistence;

    public DeleteOrderService(IOrderPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<DeleteOrderResult> DeleteAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var outcome = await _persistence.DeleteOrderAsync(orderId, cancellationToken);

        return outcome.Kind switch
        {
            DeleteOrderOutcomeKind.Success => DeleteOrderResult.Success(),
            DeleteOrderOutcomeKind.OrderNotFound =>
                DeleteOrderResult.NotFound($"Order '{orderId}' was not found."),
            DeleteOrderOutcomeKind.NotAllowed =>
                DeleteOrderResult.Conflict(
                    outcome.Message ?? "Orders in this status cannot be deleted."),
            DeleteOrderOutcomeKind.ConcurrentConflict =>
                DeleteOrderResult.Conflict(
                    outcome.Message ?? "The order or inventory was modified by another request."),
            _ => DeleteOrderResult.Conflict("Unable to delete the order.")
        };
    }
}
