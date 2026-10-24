namespace OrderManagement.Application.Orders;

public sealed class CreateOrderResult
{
    public bool IsSuccess => ErrorKind == CreateOrderErrorKind.None;
    public CreateOrderErrorKind ErrorKind { get; private init; }
    public string? ErrorMessage { get; private init; }
    public CreateOrderResponse? Response { get; private init; }

    public static CreateOrderResult Success(CreateOrderResponse response) => new()
    {
        ErrorKind = CreateOrderErrorKind.None,
        Response = response
    };

    public static CreateOrderResult ValidationError(string message) => new()
    {
        ErrorKind = CreateOrderErrorKind.Validation,
        ErrorMessage = message
    };

    public static CreateOrderResult NotFound(string message) => new()
    {
        ErrorKind = CreateOrderErrorKind.NotFound,
        ErrorMessage = message
    };
}

public enum CreateOrderErrorKind
{
    None,
    Validation,
    NotFound
}
