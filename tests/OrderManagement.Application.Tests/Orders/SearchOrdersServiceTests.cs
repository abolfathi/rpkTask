using Moq;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Tests.Orders;

public sealed class SearchOrdersServiceTests
{
    private readonly Mock<IOrderPersistence> _persistence = new();
    private readonly SearchOrdersService _sut;

    public SearchOrdersServiceTests()
    {
        _sut = new SearchOrdersService(_persistence.Object);
    }

    [Fact]
    public async Task SearchAsync_InvalidPage_ReturnsValidationError()
    {
        var result = await _sut.SearchAsync(new SearchOrdersRequest { Page = 0 });

        Assert.Equal(SearchOrdersErrorKind.Validation, result.ErrorKind);
        Assert.Contains("Page", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _persistence.Verify(
            p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_InvalidPageSize_ReturnsValidationError()
    {
        var result = await _sut.SearchAsync(new SearchOrdersRequest { PageSize = 101 });

        Assert.Equal(SearchOrdersErrorKind.Validation, result.ErrorKind);
        Assert.Contains("PageSize", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _persistence.Verify(
            p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_InvalidDateRange_ReturnsValidationError()
    {
        var result = await _sut.SearchAsync(new SearchOrdersRequest
        {
            CreatedFrom = DateTimeOffset.UtcNow,
            CreatedTo = DateTimeOffset.UtcNow.AddDays(-1)
        });

        Assert.Equal(SearchOrdersErrorKind.Validation, result.ErrorKind);
        Assert.Contains("CreatedFrom", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _persistence.Verify(
            p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_PassesCriteriaToPersistence()
    {
        var customerId = Guid.NewGuid();
        OrderSearchCriteria? captured = null;
        _persistence
            .Setup(p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<OrderSearchCriteria, CancellationToken>((c, _) => captured = c)
            .ReturnsAsync((Array.Empty<Order>(), 0));

        var from = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2024-12-31T23:59:59Z");
        var result = await _sut.SearchAsync(new SearchOrdersRequest
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            CreatedFrom = from,
            CreatedTo = to,
            Page = 2,
            PageSize = 10
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(customerId, captured.CustomerId);
        Assert.Equal(OrderStatus.Pending, captured.Status);
        Assert.Equal(from, captured.CreatedFrom);
        Assert.Equal(to, captured.CreatedTo);
        Assert.Equal(2, captured.Page);
        Assert.Equal(10, captured.PageSize);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsSuccess()
    {
        _persistence
            .Setup(p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Order>(), 0));

        var result = await _sut.SearchAsync(new SearchOrdersRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Empty(result.Response.Items);
        Assert.Equal(0, result.Response.TotalCount);
        Assert.Equal(SearchOrdersService.DefaultPage, result.Response.Page);
        Assert.Equal(SearchOrdersService.DefaultPageSize, result.Response.PageSize);
    }

    [Fact]
    public async Task SearchAsync_HappyPath_MapsPagedResponse()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            CustomerId = Guid.NewGuid(),
            Status = OrderStatus.Pending,
            TotalAmount = 15m,
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
                    UnitPrice = 15m
                }
            ]
        };

        _persistence
            .Setup(p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { order }, 42));

        var result = await _sut.SearchAsync(new SearchOrdersRequest { Page = 3, PageSize = 5 });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(42, result.Response.TotalCount);
        Assert.Equal(3, result.Response.Page);
        Assert.Equal(5, result.Response.PageSize);
        Assert.Single(result.Response.Items);
        Assert.Equal(orderId, result.Response.Items[0].OrderId);
    }
}
