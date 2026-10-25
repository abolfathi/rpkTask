using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Orders;

public enum ConfirmOrderOutcomeKind
{
    Success,
    OrderNotFound,
    InvalidState,
    InsufficientStock,
    ConcurrentConflict
}

public sealed class ConfirmOrderOutcome
{
    public ConfirmOrderOutcomeKind Kind { get; private init; }
    public Order? Order { get; private init; }
    public string? Message { get; private init; }

    public static ConfirmOrderOutcome Success(Order order) => new()
    {
        Kind = ConfirmOrderOutcomeKind.Success,
        Order = order
    };

    public static ConfirmOrderOutcome OrderNotFound() => new()
    {
        Kind = ConfirmOrderOutcomeKind.OrderNotFound,
        Message = "Order was not found."
    };

    public static ConfirmOrderOutcome InvalidState(string message) => new()
    {
        Kind = ConfirmOrderOutcomeKind.InvalidState,
        Message = message
    };

    public static ConfirmOrderOutcome InsufficientStock(string message) => new()
    {
        Kind = ConfirmOrderOutcomeKind.InsufficientStock,
        Message = message
    };

    public static ConfirmOrderOutcome ConcurrentConflict(string message) => new()
    {
        Kind = ConfirmOrderOutcomeKind.ConcurrentConflict,
        Message = message
    };
}
