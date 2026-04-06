using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/stock")]
public sealed class StockController(IOrderWorkflowService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<StockOverviewDto>>> Get([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var result = await service.GetStockOverviewAsync(warehouseId, cancellationToken);
        return Ok(result);
    }
}
