using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public sealed class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}
