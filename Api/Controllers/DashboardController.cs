using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
/// <summary>
/// Provides dashboard metric endpoints.
/// </summary>
public sealed class DashboardController(IOrderWorkflowService service) : ControllerBase
{
    /// <summary>
    /// Returns current-day dashboard summary.
    /// </summary>
    [HttpGet("today")]
    public async Task<ActionResult<DashboardDto>> Today(CancellationToken cancellationToken)
    {
        var result = await service.GetDashboardAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        return Ok(result);
    }
}
