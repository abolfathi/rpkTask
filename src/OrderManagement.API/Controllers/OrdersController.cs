using Microsoft.AspNetCore.Mvc;
using OrderManagement.Application.Orders;

namespace OrderManagement.API.Controllers;

[ApiController]
[Route("api/orders")]
[Tags("Orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderService _createOrderService;

    public OrdersController(CreateOrderService createOrderService)
    {
        _createOrderService = createOrderService;
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
