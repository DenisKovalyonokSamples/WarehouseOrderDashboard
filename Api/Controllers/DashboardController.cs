using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IOrderWorkflowService service) : ControllerBase
{
    [HttpGet("today")]
    public async Task<ActionResult<DashboardDto>> Today(CancellationToken cancellationToken)
    {
        var result = await service.GetDashboardAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        return Ok(result);
    }
}
