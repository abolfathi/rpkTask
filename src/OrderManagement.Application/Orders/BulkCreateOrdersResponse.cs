namespace OrderManagement.Application.Orders;

public sealed class BulkCreateOrdersResponse
{
    public List<CreateOrderResponse> Orders { get; set; } = [];
}
