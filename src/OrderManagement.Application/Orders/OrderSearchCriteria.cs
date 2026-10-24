using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public sealed class OrderSearchCriteria
{
    public Guid? CustomerId { get; init; }
    public OrderStatus? Status { get; init; }
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
