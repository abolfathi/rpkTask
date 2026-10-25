namespace OrderManagement.Application.Orders;

public sealed class DeleteOrderResult
{
    public bool IsSuccess => ErrorKind == DeleteOrderErrorKind.None;
    public DeleteOrderErrorKind ErrorKind { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static DeleteOrderResult Success() => new()
    {
        ErrorKind = DeleteOrderErrorKind.None
    };

    public static DeleteOrderResult NotFound(string message) => new()
    {
        ErrorKind = DeleteOrderErrorKind.NotFound,
        ErrorMessage = message
    };

    public static DeleteOrderResult Conflict(string message) => new()
    {
        ErrorKind = DeleteOrderErrorKind.Conflict,
        ErrorMessage = message
    };
}

public enum DeleteOrderErrorKind
{
    None,
    NotFound,
    Conflict
}
