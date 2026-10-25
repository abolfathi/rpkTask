using Moq;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Tests.Orders;

public sealed class UpdateOrderStatusServiceTests
{
    private readonly Mock<IOrderPersistence> _persistence = new();
    private readonly UpdateOrderStatusService _sut;

    public UpdateOrderStatusServiceTests()
    {
        _sut = new UpdateOrderStatusService(_persistence.Object);
    }

    [Fact]
    public async Task UpdateAsync_OrderNotFound_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var result = await _sut.UpdateAsync(orderId, new UpdateOrderStatusRequest { Status = OrderStatus.Confirmed });

        Assert.Equal(UpdateOrderStatusErrorKind.NotFound, result.ErrorKind);
        _persistence.Verify(p => p.ConfirmOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_InvalidTransition_ReturnsValidationError()
    {
        var order = CreateOrder(OrderStatus.Pending);
        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Shipped });

        Assert.Equal(UpdateOrderStatusErrorKind.Validation, result.ErrorKind);
        Assert.Contains("Cannot transition", result.ErrorMessage);
        _persistence.Verify(p => p.ConfirmOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _persistence.Verify(
            p => p.TryUpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_PendingToConfirmed_Success()
    {
        var order = CreateOrder(OrderStatus.Pending);
        var confirmed = CreateOrder(OrderStatus.Confirmed, order.Id, order.CustomerId);

        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _persistence
            .Setup(p => p.ConfirmOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConfirmOrderOutcome.Success(confirmed));

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Confirmed });

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, result.Response!.Status);
        _persistence.Verify(p => p.ConfirmOrderAsync(order.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ConfirmedToShipped_Success()
    {
        var order = CreateOrder(OrderStatus.Confirmed);
        var shipped = CreateOrder(OrderStatus.Shipped, order.Id, order.CustomerId);

        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _persistence
            .Setup(p => p.TryUpdateStatusAsync(order.Id, OrderStatus.Confirmed, OrderStatus.Shipped, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _persistence
            .Setup(p => p.GetOrderByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipped);

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Shipped });

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, result.Response!.Status);
        _persistence.Verify(p => p.ConfirmOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShippedToDelivered_Success()
    {
        var order = CreateOrder(OrderStatus.Shipped);
        var delivered = CreateOrder(OrderStatus.Delivered, order.Id, order.CustomerId);

        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _persistence
            .Setup(p => p.TryUpdateStatusAsync(order.Id, OrderStatus.Shipped, OrderStatus.Delivered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _persistence
            .Setup(p => p.GetOrderByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivered);

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Delivered });

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Delivered, result.Response!.Status);
    }

    [Fact]
    public async Task UpdateAsync_DeliveredToAnything_ReturnsValidationError()
    {
        var order = CreateOrder(OrderStatus.Delivered);
        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Shipped });

        Assert.Equal(UpdateOrderStatusErrorKind.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task UpdateAsync_ConfirmInsufficientStock_ReturnsConflict()
    {
        var order = CreateOrder(OrderStatus.Pending);
        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _persistence
            .Setup(p => p.ConfirmOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConfirmOrderOutcome.InsufficientStock("Insufficient inventory for product 'x'."));

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Confirmed });

        Assert.Equal(UpdateOrderStatusErrorKind.Conflict, result.ErrorKind);
        Assert.Contains("Insufficient", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAsync_ConfirmConcurrentConflict_ReturnsConflict()
    {
        var order = CreateOrder(OrderStatus.Pending);
        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _persistence
            .Setup(p => p.ConfirmOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConfirmOrderOutcome.ConcurrentConflict("The order was modified by another request."));

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Confirmed });

        Assert.Equal(UpdateOrderStatusErrorKind.Conflict, result.ErrorKind);
        Assert.Contains("modified", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_NonConfirmConcurrentConflict_ReturnsConflict()
    {
        var order = CreateOrder(OrderStatus.Confirmed);
        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _persistence
            .Setup(p => p.TryUpdateStatusAsync(order.Id, OrderStatus.Confirmed, OrderStatus.Shipped, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.UpdateAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Shipped });

        Assert.Equal(UpdateOrderStatusErrorKind.Conflict, result.ErrorKind);
    }

    [Fact]
    public async Task UpdateAsync_UndefinedStatusValue_ReturnsValidationError()
    {
        var order = CreateOrder(OrderStatus.Pending);
        _persistence
            .Setup(p => p.GetOrderForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.UpdateAsync(
            order.Id,
            new UpdateOrderStatusRequest { Status = (OrderStatus)999 });

        Assert.Equal(UpdateOrderStatusErrorKind.Validation, result.ErrorKind);
        Assert.Contains("not a valid order status", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _persistence.Verify(p => p.ConfirmOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Order CreateOrder(OrderStatus status, Guid? id = null, Guid? customerId = null)
    {
        var orderId = id ?? Guid.NewGuid();
        return new Order
        {
            Id = orderId,
            CustomerId = customerId ?? Guid.NewGuid(),
            Status = status,
            TotalAmount = 10m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    Quantity = 1,
                    UnitPrice = 10m
                }
            ]
        };
    }
}
