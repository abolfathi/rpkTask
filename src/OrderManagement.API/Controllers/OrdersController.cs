using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.API.Auth;
using OrderManagement.Application.Orders;

namespace OrderManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
[Tags("Orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderService _createOrderService;
    private readonly GetOrderService _getOrderService;
    private readonly SearchOrdersService _searchOrdersService;
    private readonly UpdateOrderStatusService _updateOrderStatusService;
    private readonly DeleteOrderService _deleteOrderService;

    public OrdersController(
        CreateOrderService createOrderService,
        GetOrderService getOrderService,
        SearchOrdersService searchOrdersService,
        UpdateOrderStatusService updateOrderStatusService,
        DeleteOrderService deleteOrderService)
    {
        _createOrderService = createOrderService;
        _getOrderService = getOrderService;
        _searchOrdersService = searchOrdersService;
        _updateOrderStatusService = updateOrderStatusService;
        _deleteOrderService = deleteOrderService;
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get order by ID")]
    [EndpointDescription("Returns a single order including its line items. Requires authentication (Admin or User).")]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        "Results are sorted by CreatedAt descending, then Id descending, and paginated. " +
        "Requires authentication (Admin or User).")]
    [ProducesResponseType(typeof(SearchOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    [HttpPatch("{id:guid}/status")]
    [EndpointSummary("Update order status")]
    [EndpointDescription(
        "Advances an order along Pending → Confirmed → Shipped → Delivered. " +
        "Confirming validates inventory for all line items and deducts stock atomically. " +
        "Invalid transitions are rejected. Cancellation is not supported. " +
        "Requires authentication (Admin or User).")]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateOrderStatusService.UpdateAsync(id, request, cancellationToken);

        return result.ErrorKind switch
        {
            UpdateOrderStatusErrorKind.None => Ok(result.Response),
            UpdateOrderStatusErrorKind.Validation => BadRequest(new { error = result.ErrorMessage }),
            UpdateOrderStatusErrorKind.NotFound => NotFound(new { error = result.ErrorMessage }),
            UpdateOrderStatusErrorKind.Conflict => Conflict(new { error = result.ErrorMessage }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [EndpointSummary("Delete order")]
    [EndpointDescription(
        "Soft-deletes an order (IsDeleted/DeletedAt). Admin only. Order and line items are retained. " +
        "Pending: mark deleted with no inventory change. Confirmed: mark deleted then restore deducted " +
        "stock in one transaction. Soft-deleted orders are hidden from Get/Search. " +
        "Shipped and Delivered orders are rejected with 409 Conflict.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deleteOrderService.DeleteAsync(id, cancellationToken);

        return result.ErrorKind switch
        {
            DeleteOrderErrorKind.None => NoContent(),
            DeleteOrderErrorKind.NotFound => NotFound(new { error = result.ErrorMessage }),
            DeleteOrderErrorKind.Conflict => Conflict(new { error = result.ErrorMessage }),
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
        "and persists the order as Pending. Inventory is not checked or deducted. " +
        "Requires authentication (Admin or User).")]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
