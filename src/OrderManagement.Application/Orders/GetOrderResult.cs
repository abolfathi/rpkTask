namespace OrderManagement.Application.Orders;

public sealed class GetOrderResult
{
    public bool IsSuccess => ErrorKind == GetOrderErrorKind.None;
    public GetOrderErrorKind ErrorKind { get; private init; }
    public string? ErrorMessage { get; private init; }
    public CreateOrderResponse? Response { get; private init; }

    public static GetOrderResult Success(CreateOrderResponse response) => new()
    {
        ErrorKind = GetOrderErrorKind.None,
        Response = response
    };

    public static GetOrderResult NotFound(string message) => new()
    {
        ErrorKind = GetOrderErrorKind.NotFound,
        ErrorMessage = message
    };
}

public enum GetOrderErrorKind
{
    None,
    NotFound
}
