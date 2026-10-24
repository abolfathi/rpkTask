using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public sealed class CreateOrderService
{
    private readonly IOrderPersistence _persistence;

    public CreateOrderService(IOrderPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<CreateOrderResult> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return CreateOrderResult.ValidationError("Order must contain at least one item.");
        }

        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return CreateOrderResult.ValidationError("All item quantities must be greater than zero.");
        }

        var productIds = request.Items.Select(i => i.ProductId).ToList();
        if (productIds.Count != productIds.Distinct().Count())
        {
            return CreateOrderResult.ValidationError("Duplicate product IDs are not allowed.");
        }

        if (!await _persistence.CustomerExistsAsync(request.CustomerId, cancellationToken))
        {
            return CreateOrderResult.NotFound($"Customer '{request.CustomerId}' was not found.");
        }

        var prices = await _persistence.GetProductPricesAsync(productIds, cancellationToken);
        var priceById = prices.ToDictionary(p => p.ProductId, p => p.Price);
        var missingProductIds = productIds.Where(id => !priceById.ContainsKey(id)).ToList();
        if (missingProductIds.Count > 0)
        {
            return CreateOrderResult.NotFound(
                $"Product(s) not found: {string.Join(", ", missingProductIds)}.");
        }

        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        var orderItems = request.Items.Select(item =>
        {
            var unitPrice = priceById[item.ProductId];
            return new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice
            };
        }).ToList();

        var totalAmount = orderItems.Sum(i => i.Quantity * i.UnitPrice);
        var order = new Order
        {
            Id = orderId,
            CustomerId = request.CustomerId,
            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            CreatedAt = now,
            UpdatedAt = now,
            Items = orderItems
        };

        await _persistence.SaveOrderAsync(order, cancellationToken);

        return CreateOrderResult.Success(new CreateOrderResponse
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            Items = orderItems.Select(i => new CreateOrderItemResponse
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.Quantity * i.UnitPrice
            }).ToList()
        });
    }
}
