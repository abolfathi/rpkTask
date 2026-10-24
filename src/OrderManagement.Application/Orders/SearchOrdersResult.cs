namespace OrderManagement.Application.Orders;

public sealed class SearchOrdersResult
{
    public bool IsSuccess => ErrorKind == SearchOrdersErrorKind.None;
    public SearchOrdersErrorKind ErrorKind { get; private init; }
    public string? ErrorMessage { get; private init; }
    public SearchOrdersResponse? Response { get; private init; }

    public static SearchOrdersResult Success(SearchOrdersResponse response) => new()
    {
        ErrorKind = SearchOrdersErrorKind.None,
        Response = response
    };

    public static SearchOrdersResult ValidationError(string message) => new()
    {
        ErrorKind = SearchOrdersErrorKind.Validation,
        ErrorMessage = message
    };
}

public enum SearchOrdersErrorKind
{
    None,
    Validation
}
