namespace OrderManagement.Application.Orders;

public enum DeleteOrderOutcomeKind
{
    Success,
    OrderNotFound,
    ConcurrentConflict
}

public sealed class DeleteOrderOutcome
{
    public DeleteOrderOutcomeKind Kind { get; private init; }
    public string? Message { get; private init; }

    public static DeleteOrderOutcome Success() => new()
    {
        Kind = DeleteOrderOutcomeKind.Success
    };

    public static DeleteOrderOutcome OrderNotFound() => new()
    {
        Kind = DeleteOrderOutcomeKind.OrderNotFound,
        Message = "Order was not found."
    };

    public static DeleteOrderOutcome ConcurrentConflict(string message) => new()
    {
        Kind = DeleteOrderOutcomeKind.ConcurrentConflict,
        Message = message
    };
}
