using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Orders;

public sealed class UpdateOrderStatusService
{
    private readonly IOrderPersistence _persistence;

    public UpdateOrderStatusService(IOrderPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<UpdateOrderStatusResult> UpdateAsync(
        Guid orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return UpdateOrderStatusResult.ValidationError(
                $"Status '{request.Status}' is not a valid order status.");
        }

        var order = await _persistence.GetOrderForUpdateAsync(orderId, cancellationToken);
        if (order is null)
        {
            return UpdateOrderStatusResult.NotFound($"Order '{orderId}' was not found.");
        }

        if (!OrderStatusTransitionPolicy.CanTransition(order.Status, request.Status))
        {
            return UpdateOrderStatusResult.ValidationError(
                $"Cannot transition order from '{order.Status}' to '{request.Status}'.");
        }

        if (request.Status == OrderStatus.Confirmed)
        {
            var confirmOutcome = await _persistence.ConfirmOrderAsync(orderId, cancellationToken);
            return confirmOutcome.Kind switch
            {
                ConfirmOrderOutcomeKind.Success =>
                    UpdateOrderStatusResult.Success(OrderResponseMapper.ToResponse(confirmOutcome.Order!)),
                ConfirmOrderOutcomeKind.OrderNotFound =>
                    UpdateOrderStatusResult.NotFound($"Order '{orderId}' was not found."),
                ConfirmOrderOutcomeKind.InvalidState =>
                    UpdateOrderStatusResult.ValidationError(
                        confirmOutcome.Message ?? "Order is not in a confirmable state."),
                ConfirmOrderOutcomeKind.InsufficientStock =>
                    UpdateOrderStatusResult.Conflict(
                        confirmOutcome.Message ?? "Insufficient inventory to confirm the order."),
                ConfirmOrderOutcomeKind.ConcurrentConflict =>
                    UpdateOrderStatusResult.Conflict(
                        confirmOutcome.Message ?? "The order was modified by another request."),
                _ => UpdateOrderStatusResult.Conflict("Unable to confirm the order.")
            };
        }

        var updated = await _persistence.TryUpdateStatusAsync(
            orderId,
            order.Status,
            request.Status,
            cancellationToken);

        if (!updated)
        {
            return UpdateOrderStatusResult.Conflict(
                "The order status was modified by another request. Refresh and try again.");
        }

        var refreshed = await _persistence.GetOrderByIdAsync(orderId, cancellationToken);
        if (refreshed is null)
        {
            return UpdateOrderStatusResult.NotFound($"Order '{orderId}' was not found.");
        }

        return UpdateOrderStatusResult.Success(OrderResponseMapper.ToResponse(refreshed));
    }
}
