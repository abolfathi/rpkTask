using Moq;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Tests.Orders;

public sealed class BulkCreateOrdersServiceTests
{
    private readonly Mock<IOrderPersistence> _persistence = new();
    private readonly BulkCreateOrdersService _sut;

    public BulkCreateOrdersServiceTests()
    {
        _sut = new BulkCreateOrdersService(_persistence.Object);
    }

    [Fact]
    public async Task CreateAsync_EmptyBatch_ReturnsValidationError()
    {
        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest { Orders = [] });

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains(result.Errors, e => e.Index == -1);
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _persistence.Verify(
            p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ExceedsMaxBatchSize_ReturnsValidationError()
    {
        var productId = Guid.NewGuid();
        var orders = Enumerable.Range(0, BulkCreateOrdersService.MaxBulkOrderCount + 1)
            .Select(_ => new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
            })
            .ToList();

        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest { Orders = orders });

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains(
            result.Errors,
            e => e.Index == -1 && e.Error.Contains("maximum of", StringComparison.OrdinalIgnoreCase));
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _persistence.Verify(
            p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _persistence.Verify(
            p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmptyItems_ReturnsIndexedValidationError()
    {
        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest
        {
            Orders =
            [
                new CreateOrderRequest { CustomerId = Guid.NewGuid(), Items = [] }
            ]
        });

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains(result.Errors, e => e.Index == 0 && e.Error.Contains("at least one item", StringComparison.OrdinalIgnoreCase));
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NonPositiveQuantity_ReturnsIndexedValidationError()
    {
        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest
        {
            Orders =
            [
                new CreateOrderRequest
                {
                    CustomerId = Guid.NewGuid(),
                    Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 0 }]
                }
            ]
        });

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains(result.Errors, e => e.Index == 0 && e.Error.Contains("quantities", StringComparison.OrdinalIgnoreCase));
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateProductIds_ReturnsIndexedValidationError()
    {
        var productId = Guid.NewGuid();
        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest
        {
            Orders =
            [
                new CreateOrderRequest
                {
                    CustomerId = Guid.NewGuid(),
                    Items =
                    [
                        new CreateOrderItemRequest { ProductId = productId, Quantity = 1 },
                        new CreateOrderItemRequest { ProductId = productId, Quantity = 2 }
                    ]
                }
            ]
        });

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains(result.Errors, e => e.Index == 0 && e.Error.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        _persistence.Verify(
            p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_CustomerNotFound_ReturnsIndexedNotFound()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _persistence
            .Setup(p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _persistence
            .Setup(p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProductPrice(productId, 10m)]);

        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest
        {
            Orders =
            [
                new CreateOrderRequest
                {
                    CustomerId = customerId,
                    Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
                }
            ]
        });

        Assert.Equal(CreateOrderErrorKind.NotFound, result.ErrorKind);
        Assert.Contains(result.Errors, e => e.Index == 0 && e.Error.Contains(customerId.ToString(), StringComparison.Ordinal));
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ProductNotFound_ReturnsIndexedNotFound()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _persistence
            .Setup(p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customerId]);
        _persistence
            .Setup(p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest
        {
            Orders =
            [
                new CreateOrderRequest
                {
                    CustomerId = customerId,
                    Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
                }
            ]
        });

        Assert.Equal(CreateOrderErrorKind.NotFound, result.ErrorKind);
        Assert.Contains(result.Errors, e => e.Index == 0 && e.Error.Contains(productId.ToString(), StringComparison.Ordinal));
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_MixedInvalidOrders_DoesNotSave_ReturnsMultipleErrors()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var missingCustomerId = Guid.NewGuid();

        _persistence
            .Setup(p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customerId]);
        _persistence
            .Setup(p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProductPrice(productId, 5m)]);

        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest
        {
            Orders =
            [
                new CreateOrderRequest { CustomerId = customerId, Items = [] },
                new CreateOrderRequest
                {
                    CustomerId = missingCustomerId,
                    Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
                }
            ]
        });

        Assert.Equal(CreateOrderErrorKind.Validation, result.ErrorKind);
        Assert.Contains(result.Errors, e => e.Index == 0);
        Assert.Contains(result.Errors, e => e.Index == 1);
        _persistence.Verify(
            p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidBatch_SavesPendingOrdersOnceWithDbPrices()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        _persistence
            .Setup(p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customerA, customerB]);
        _persistence
            .Setup(p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProductPrice(productA, 10m),
                new ProductPrice(productB, 2.5m)
            ]);
        _persistence
            .Setup(p => p.SaveOrdersAsync(It.IsAny<IReadOnlyList<Order>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(new BulkCreateOrdersRequest
        {
            Orders =
            [
                new CreateOrderRequest
                {
                    CustomerId = customerA,
                    Items =
                    [
                        new CreateOrderItemRequest { ProductId = productA, Quantity = 2 },
                        new CreateOrderItemRequest { ProductId = productB, Quantity = 1 }
                    ]
                },
                new CreateOrderRequest
                {
                    CustomerId = customerB,
                    Items = [new CreateOrderItemRequest { ProductId = productB, Quantity = 4 }]
                }
            ]
        });

        Assert.Equal(CreateOrderErrorKind.None, result.ErrorKind);
        Assert.NotNull(result.Response);
        Assert.Equal(2, result.Response.Orders.Count);
        Assert.Equal(22.5m, result.Response.Orders[0].TotalAmount);
        Assert.Equal(10m, result.Response.Orders[1].TotalAmount);
        Assert.All(result.Response.Orders, o => Assert.Equal(OrderStatus.Pending, o.Status));

        _persistence.Verify(
            p => p.GetExistingCustomerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _persistence.Verify(
            p => p.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _persistence.Verify(
            p => p.SaveOrdersAsync(
                It.Is<IReadOnlyList<Order>>(list =>
                    list.Count == 2
                    && list.All(o => o.Status == OrderStatus.Pending)
                    && list[0].TotalAmount == 22.5m
                    && list[1].TotalAmount == 10m),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _persistence.Verify(
            p => p.SaveOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
