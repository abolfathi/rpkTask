using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Orders;

public sealed class SearchOrdersService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly IOrderPersistence _persistence;

    public SearchOrdersService(IOrderPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<SearchOrdersResult> SearchAsync(
        SearchOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = request.Page ?? DefaultPage;
        var pageSize = request.PageSize ?? DefaultPageSize;

        if (page < 1)
        {
            return SearchOrdersResult.ValidationError("Page must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return SearchOrdersResult.ValidationError($"PageSize must be between 1 and {MaxPageSize}.");
        }

        if (request.Status is { } status && !Enum.IsDefined(status))
        {
            return SearchOrdersResult.ValidationError($"Status '{status}' is not a valid order status.");
        }

        if (request.CreatedFrom is { } from && request.CreatedTo is { } to && from > to)
        {
            return SearchOrdersResult.ValidationError("CreatedFrom must be less than or equal to CreatedTo.");
        }

        var criteria = new OrderSearchCriteria
        {
            CustomerId = request.CustomerId,
            Status = request.Status,
            CreatedFrom = request.CreatedFrom,
            CreatedTo = request.CreatedTo,
            Page = page,
            PageSize = pageSize
        };

        var (orders, totalCount) = await _persistence.SearchOrdersAsync(criteria, cancellationToken);

        return SearchOrdersResult.Success(new SearchOrdersResponse
        {
            Items = orders.Select(OrderResponseMapper.ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }
}
