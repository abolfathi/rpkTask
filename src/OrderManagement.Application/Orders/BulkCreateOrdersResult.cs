namespace OrderManagement.Application.Orders;

public sealed class BulkCreateOrderError
{
    public int Index { get; init; }
    public string Error { get; init; } = string.Empty;
}

public sealed class BulkCreateOrdersResult
{
    public bool IsSuccess => ErrorKind == CreateOrderErrorKind.None;
    public CreateOrderErrorKind ErrorKind { get; private init; }
    public IReadOnlyList<BulkCreateOrderError> Errors { get; private init; } = [];
    public BulkCreateOrdersResponse? Response { get; private init; }

    public static BulkCreateOrdersResult Success(BulkCreateOrdersResponse response) => new()
    {
        ErrorKind = CreateOrderErrorKind.None,
        Response = response
    };

    public static BulkCreateOrdersResult Failure(
        CreateOrderErrorKind errorKind,
        IReadOnlyList<BulkCreateOrderError> errors) => new()
    {
        ErrorKind = errorKind,
        Errors = errors
    };
}
