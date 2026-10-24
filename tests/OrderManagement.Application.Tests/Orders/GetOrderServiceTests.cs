using Moq;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Tests.Orders;

public sealed class GetOrderServiceTests
{
    private readonly Mock<IOrderPersistence> _persistence = new();
    private readonly GetOrderService _sut;

    public GetOrderServiceTests()
    {
        _sut = new GetOrderService(_persistence.Object);
    }

    [Fact]
    public async Task GetByIdAsync_OrderExists_ReturnsSuccess()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            CustomerId = Guid.NewGuid(),
            Status = OrderStatus.Pending,
            TotalAmount = 20m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = productId,
                    Quantity = 2,
                    UnitPrice = 10m
                }
            ]
        };

        _persistence
            .Setup(p => p.GetOrderByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _sut.GetByIdAsync(orderId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(orderId, result.Response.OrderId);
        Assert.Equal(OrderStatus.Pending, result.Response.Status);
        Assert.Single(result.Response.Items);
        Assert.Equal(20m, result.Response.Items[0].LineTotal);
    }

    [Fact]
    public async Task GetByIdAsync_OrderMissing_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        _persistence
            .Setup(p => p.GetOrderByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var result = await _sut.GetByIdAsync(orderId);

        Assert.Equal(GetOrderErrorKind.NotFound, result.ErrorKind);
        Assert.Contains(orderId.ToString(), result.ErrorMessage);
    }
}
