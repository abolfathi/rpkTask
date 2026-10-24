namespace OrderManagement.Application.Orders;

public sealed class SearchOrdersResponse
{
    public List<CreateOrderResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
