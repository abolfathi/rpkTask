using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public sealed class BulkCreateOrdersService
{
    public const int MaxBulkOrderCount = 100;

    private readonly IOrderPersistence _persistence;

    public BulkCreateOrdersService(IOrderPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<BulkCreateOrdersResult> CreateAsync(
        BulkCreateOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Orders is null || request.Orders.Count == 0)
        {
            return BulkCreateOrdersResult.Failure(
                CreateOrderErrorKind.Validation,
                [new BulkCreateOrderError { Index = -1, Error = "At least one order is required." }]);
        }

        if (request.Orders.Count > MaxBulkOrderCount)
        {
            return BulkCreateOrdersResult.Failure(
                CreateOrderErrorKind.Validation,
                [
                    new BulkCreateOrderError
                    {
                        Index = -1,
                        Error = $"A maximum of {MaxBulkOrderCount} orders is allowed per request."
                    }
                ]);
        }

        var errors = new List<BulkCreateOrderError>();
        var orders = request.Orders;
        var validIndices = new List<int>();

        for (var i = 0; i < orders.Count; i++)
        {
            var itemError = OrderCreateValidation.ValidateItems(orders[i].Items);
            if (itemError is not null)
            {
                errors.Add(new BulkCreateOrderError { Index = i, Error = itemError });
            }
            else
            {
                validIndices.Add(i);
            }
        }

        if (validIndices.Count == 0)
        {
            return BulkCreateOrdersResult.Failure(CreateOrderErrorKind.Validation, errors);
        }

        var customerIds = validIndices.Select(i => orders[i].CustomerId).Distinct().ToList();
        var existingCustomers = await _persistence.GetExistingCustomerIdsAsync(customerIds, cancellationToken);
        var existingCustomerSet = existingCustomers.ToHashSet();

        foreach (var i in validIndices)
        {
            if (!existingCustomerSet.Contains(orders[i].CustomerId))
            {
                errors.Add(new BulkCreateOrderError
                {
                    Index = i,
                    Error = $"Customer '{orders[i].CustomerId}' was not found."
                });
            }
        }

        var allProductIds = validIndices
            .SelectMany(i => orders[i].Items.Select(item => item.ProductId))
            .Distinct()
            .ToList();

        var prices = await _persistence.GetProductPricesAsync(allProductIds, cancellationToken);
        var priceById = prices.ToDictionary(p => p.ProductId, p => p.Price);

        foreach (var i in validIndices)
        {
            var missingProductIds = orders[i].Items
                .Select(item => item.ProductId)
                .Where(id => !priceById.ContainsKey(id))
                .Distinct()
                .ToList();

            if (missingProductIds.Count > 0)
            {
                errors.Add(new BulkCreateOrderError
                {
                    Index = i,
                    Error = $"Product(s) not found: {string.Join(", ", missingProductIds)}."
                });
            }
        }

        if (errors.Count > 0)
        {
            var errorKind = errors.Any(IsValidationMessage)
                ? CreateOrderErrorKind.Validation
                : CreateOrderErrorKind.NotFound;

            return BulkCreateOrdersResult.Failure(errorKind, errors);
        }

        var now = DateTimeOffset.UtcNow;
        var entities = new List<Order>(orders.Count);
        var responses = new List<CreateOrderResponse>(orders.Count);

        foreach (var orderRequest in orders)
        {
            var orderId = Guid.NewGuid();
            var orderItems = orderRequest.Items.Select(item =>
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
                CustomerId = orderRequest.CustomerId,
                Status = OrderStatus.Pending,
                TotalAmount = totalAmount,
                CreatedAt = now,
                UpdatedAt = now,
                Items = orderItems
            };

            entities.Add(order);
            responses.Add(new CreateOrderResponse
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

        await _persistence.SaveOrdersAsync(entities, cancellationToken);

        return BulkCreateOrdersResult.Success(new BulkCreateOrdersResponse { Orders = responses });
    }

    private static bool IsValidationMessage(BulkCreateOrderError error) =>
        error.Error.Contains("must contain", StringComparison.Ordinal)
        || error.Error.Contains("quantities", StringComparison.Ordinal)
        || error.Error.Contains("Duplicate", StringComparison.Ordinal)
        || error.Error.Contains("At least one order", StringComparison.Ordinal)
        || error.Error.Contains("maximum of", StringComparison.Ordinal);
}
