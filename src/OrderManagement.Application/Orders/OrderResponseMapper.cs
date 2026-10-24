using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Orders;

internal static class OrderResponseMapper
{
    public static CreateOrderResponse ToResponse(Order order) => new()
    {
        OrderId = order.Id,
        CustomerId = order.CustomerId,
        Status = order.Status,
        TotalAmount = order.TotalAmount,
        CreatedAt = order.CreatedAt,
        Items = order.Items
            .Select(i => new CreateOrderItemResponse
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.Quantity * i.UnitPrice
            })
            .ToList()
    };
}
