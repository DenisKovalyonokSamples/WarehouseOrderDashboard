using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SampleController : ControllerBase
{
    private readonly IAppService _service;
    public SampleController(IAppService service) => _service = service;

    [HttpGet]
    public IActionResult Get() => Ok(new { Message = _service.GetMessage() });
}
