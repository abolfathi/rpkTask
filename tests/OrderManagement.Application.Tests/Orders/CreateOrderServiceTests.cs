using Moq;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Tests.Orders;

public sealed class CreateOrderServiceTests
{
    private readonly Mock<IOrderPersistence> _persistence = new();
    private readonly CreateOrderService _sut;

    public CreateOrderServiceTests()
    {
        _sut = new CreateOrderService(_persistence.Object);
    }

    [Fact]
    public async Task CreateAsync_EmptyItems_ReturnsValidationError()
    {
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = []
        };

        var result = await _sut.CreateAsync(request);

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains("at least one item", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _persistence.Verify(p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NonPositiveQuantity_ReturnsValidationError()
    {
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items =
            [
                new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 0 }
            ]
        };

        var result = await _sut.CreateAsync(request);

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains("quantities", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _persistence.Verify(p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateProductIds_ReturnsValidationError()
    {
        var productId = Guid.NewGuid();
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items =
            [
                new CreateOrderItemRequest { ProductId = productId, Quantity = 1 },
                new CreateOrderItemRequest { ProductId = productId, Quantity = 2 }
            ]
        };

        var result = await _sut.CreateAsync(request);

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains("Duplicate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _persistence.Verify(p => p.CustomerExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _persistence.Verify(p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_CustomerNotFound_ReturnsNotFound()
    {
        var customerId = Guid.NewGuid();
        var request = new CreateOrderRequest
        {
            CustomerId = customerId,
            Items =
            [
                new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }
            ]
        };

        _persistence
            .Setup(p => p.CustomerExistsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.CreateAsync(request);

        Assert.Equal(CreateOrderErrorKind.NotFound, result.ErrorKind);
        Assert.Contains(customerId.ToString(), result.ErrorMessage);
        _persistence.Verify(p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ProductNotFound_ReturnsNotFound()
    {
        var customerId = Guid.NewGuid();
        var missingProductId = Guid.NewGuid();
        var request = new CreateOrderRequest
        {
            CustomerId = customerId,
            Items =
            [
                new CreateOrderItemRequest { ProductId = missingProductId, Quantity = 2 }
            ]
        };

        _persistence
            .Setup(p => p.CustomerExistsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _persistence
            .Setup(p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.CreateAsync(request);

        Assert.Equal(CreateOrderErrorKind.NotFound, result.ErrorKind);
        Assert.Contains(missingProductId.ToString(), result.ErrorMessage);
        _persistence.Verify(p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_SavesPendingOrderWithDbPrices()
    {
        var customerId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var request = new CreateOrderRequest
        {
            CustomerId = customerId,
            Items =
            [
                new CreateOrderItemRequest { ProductId = productA, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = productB, Quantity = 3 }
            ]
        };

        _persistence
            .Setup(p => p.CustomerExistsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _persistence
            .Setup(p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProductPrice(productA, 10.00m),
                new ProductPrice(productB, 5.50m)
            ]);

        Order? savedOrder = null;
        _persistence
            .Setup(p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => savedOrder = order)
            .Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(OrderStatus.Pending, result.Response.Status);
        Assert.Equal(36.50m, result.Response.TotalAmount);
        Assert.Equal(2, result.Response.Items.Count);
        Assert.Contains(result.Response.Items, i => i.ProductId == productA && i.UnitPrice == 10.00m && i.LineTotal == 20.00m);
        Assert.Contains(result.Response.Items, i => i.ProductId == productB && i.UnitPrice == 5.50m && i.LineTotal == 16.50m);

        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.Pending, savedOrder.Status);
        Assert.Equal(36.50m, savedOrder.TotalAmount);
        Assert.Equal(2, savedOrder.Items.Count);
        _persistence.Verify(p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
