using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;
using Warehouse.Domain;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderWorkflowService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderDetailsDto>> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { orderId = created.Id }, created);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> List(
        [FromQuery] DateTime? createdFromUtc,
        [FromQuery] DateTime? createdToUtc,
        [FromQuery] OrderStatus? status,
        [FromQuery] int? customerId,
        [FromQuery] int? warehouseId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = new OrderQuery
        {
            CreatedFromUtc = createdFromUtc,
            CreatedToUtc = createdToUtc,
            Status = status,
            CustomerId = customerId,
            WarehouseId = warehouseId,
            Search = search,
            Page = page,
            PageSize = pageSize
        };

        var result = await service.GetOrdersAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{orderId:int}")]
    public async Task<ActionResult<OrderDetailsDto>> GetById(int orderId, CancellationToken cancellationToken)
    {
        var order = await service.GetOrderByIdAsync(orderId, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPatch("{orderId:int}/status")]
    public async Task<ActionResult<OrderDetailsDto>> ChangeStatus(int orderId, [FromBody] ChangeOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ChangeStatusAsync(orderId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{orderId:int}/cancel")]
    public async Task<IActionResult> Cancel(int orderId, [FromQuery] long expectedVersion, CancellationToken cancellationToken)
    {
        await service.CancelOrderAsync(orderId, expectedVersion, cancellationToken);
        return NoContent();
    }
}
