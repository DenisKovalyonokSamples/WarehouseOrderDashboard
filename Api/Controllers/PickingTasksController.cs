using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/picking-tasks")]
/// <summary>
/// Provides endpoints for creating and progressing picking tasks.
/// </summary>
public sealed class PickingTasksController(IOrderWorkflowService service) : ControllerBase
{
    /// <summary>
    /// Creates a new picking task from selected orders.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PickingTaskDto>> Create([FromBody] CreatePickingTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreatePickingTaskAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }

    /// <summary>
    /// Completes picked quantity on a picking task line.
    /// </summary>
    [HttpPatch("lines/{lineId:int}/complete")]
    public async Task<ActionResult<PickingTaskDto>> CompleteLine(int lineId, [FromBody] CompletePickingLineRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CompletePickingLineAsync(lineId, request, cancellationToken);
        return Ok(result);
    }
}
