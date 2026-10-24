using Microsoft.AspNetCore.Mvc;
using OrderManagement.Application.Orders;

namespace OrderManagement.API.Controllers;

[ApiController]
[Route("api/orders")]
[Tags("Orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderService _createOrderService;
    private readonly GetOrderService _getOrderService;
    private readonly SearchOrdersService _searchOrdersService;

    public OrdersController(
        CreateOrderService createOrderService,
        GetOrderService getOrderService,
        SearchOrdersService searchOrdersService)
    {
        _createOrderService = createOrderService;
        _getOrderService = getOrderService;
        _searchOrdersService = searchOrdersService;
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get order by ID")]
    [EndpointDescription("Returns a single order including its line items.")]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getOrderService.GetByIdAsync(id, cancellationToken);

        return result.ErrorKind switch
        {
            GetOrderErrorKind.None => Ok(result.Response),
            GetOrderErrorKind.NotFound => NotFound(new { error = result.ErrorMessage }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet]
    [EndpointSummary("Search orders")]
    [EndpointDescription(
        "Searches orders with optional filters for customer, status, and created date range. " +
        "Results are sorted by CreatedAt descending, then Id descending, and paginated.")]
    [ProducesResponseType(typeof(SearchOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] SearchOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _searchOrdersService.SearchAsync(request, cancellationToken);

        return result.ErrorKind switch
        {
            SearchOrdersErrorKind.None => Ok(result.Response),
            SearchOrdersErrorKind.Validation => BadRequest(new { error = result.ErrorMessage }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Creates a new order in Pending status.
    /// </summary>
    [HttpPost]
    [EndpointSummary("Create order")]
    [EndpointDescription(
        "Creates an order for an existing customer with one or more products. " +
        "Validates quantities and duplicate product IDs, prices items from the database, " +
        "and persists the order as Pending. Inventory is not checked or deducted.")]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createOrderService.CreateAsync(request, cancellationToken);

        return result.ErrorKind switch
        {
            CreateOrderErrorKind.None => Created(
                $"/api/orders/{result.Response!.OrderId}",
                result.Response),
            CreateOrderErrorKind.Validation => BadRequest(new { error = result.ErrorMessage }),
            CreateOrderErrorKind.NotFound => NotFound(new { error = result.ErrorMessage }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
