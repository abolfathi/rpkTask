using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public sealed class SearchOrdersRequest
{
    public Guid? CustomerId { get; set; }
    public OrderStatus? Status { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
