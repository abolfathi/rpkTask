using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public sealed class CreateOrderResponse
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<CreateOrderItemResponse> Items { get; set; } = [];
}

public sealed class CreateOrderItemResponse
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
