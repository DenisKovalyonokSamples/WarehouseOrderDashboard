using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/stock")]
/// <summary>
/// Provides stock overview endpoints.
/// </summary>
public sealed class StockController(IOrderWorkflowService service) : ControllerBase
{
    /// <summary>
    /// Returns stock overview, optionally filtered by warehouse.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<StockOverviewDto>>> Get([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var result = await service.GetStockOverviewAsync(warehouseId, cancellationToken);
        return Ok(result);
    }
}
