namespace OrderManagement.Application.Orders;

public sealed class BulkCreateOrdersRequest
{
    public List<CreateOrderRequest> Orders { get; set; } = [];
}
