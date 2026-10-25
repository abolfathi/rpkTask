using Moq;
using OrderManagement.Application.Orders;

namespace OrderManagement.Application.Tests.Orders;

public sealed class DeleteOrderServiceTests
{
    private readonly Mock<IOrderPersistence> _persistence = new();
    private readonly DeleteOrderService _sut;

    public DeleteOrderServiceTests()
    {
        _sut = new DeleteOrderService(_persistence.Object);
    }

    [Fact]
    public async Task DeleteAsync_Success_ReturnsSuccess()
    {
        var orderId = Guid.NewGuid();
        _persistence
            .Setup(p => p.DeleteOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteOrderOutcome.Success());

        var result = await _sut.DeleteAsync(orderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(DeleteOrderErrorKind.None, result.ErrorKind);
    }

    [Fact]
    public async Task DeleteAsync_OrderNotFound_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        _persistence
            .Setup(p => p.DeleteOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteOrderOutcome.OrderNotFound());

        var result = await _sut.DeleteAsync(orderId);

        Assert.Equal(DeleteOrderErrorKind.NotFound, result.ErrorKind);
        Assert.Contains(orderId.ToString(), result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteAsync_ConcurrentConflict_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        _persistence
            .Setup(p => p.DeleteOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteOrderOutcome.ConcurrentConflict("Inventory could not be restored."));

        var result = await _sut.DeleteAsync(orderId);

        Assert.Equal(DeleteOrderErrorKind.Conflict, result.ErrorKind);
        Assert.Contains("Inventory", result.ErrorMessage);
    }
}
