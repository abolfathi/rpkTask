namespace OrderManagement.Application.Orders;

public sealed class CreateOrderRequest
{
    public Guid CustomerId { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

public sealed class CreateOrderItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
