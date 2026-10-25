namespace OrderManagement.Application.Orders;

public sealed class UpdateOrderStatusResult
{
    public bool IsSuccess => ErrorKind == UpdateOrderStatusErrorKind.None;
    public UpdateOrderStatusErrorKind ErrorKind { get; private init; }
    public string? ErrorMessage { get; private init; }
    public CreateOrderResponse? Response { get; private init; }

    public static UpdateOrderStatusResult Success(CreateOrderResponse response) => new()
    {
        ErrorKind = UpdateOrderStatusErrorKind.None,
        Response = response
    };

    public static UpdateOrderStatusResult NotFound(string message) => new()
    {
        ErrorKind = UpdateOrderStatusErrorKind.NotFound,
        ErrorMessage = message
    };

    public static UpdateOrderStatusResult ValidationError(string message) => new()
    {
        ErrorKind = UpdateOrderStatusErrorKind.Validation,
        ErrorMessage = message
    };

    public static UpdateOrderStatusResult Conflict(string message) => new()
    {
        ErrorKind = UpdateOrderStatusErrorKind.Conflict,
        ErrorMessage = message
    };
}

public enum UpdateOrderStatusErrorKind
{
    None,
    NotFound,
    Validation,
    Conflict
}
