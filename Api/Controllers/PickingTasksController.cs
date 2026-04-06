using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/picking-tasks")]
public sealed class PickingTasksController(IOrderWorkflowService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PickingTaskDto>> Create([FromBody] CreatePickingTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreatePickingTaskAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("lines/{lineId:int}/complete")]
    public async Task<ActionResult<PickingTaskDto>> CompleteLine(int lineId, [FromBody] CompletePickingLineRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CompletePickingLineAsync(lineId, request, cancellationToken);
        return Ok(result);
    }
}
