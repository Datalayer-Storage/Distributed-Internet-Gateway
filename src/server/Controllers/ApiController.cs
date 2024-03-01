using Microsoft.AspNetCore.Mvc;

namespace dig.server;

[ApiController]
[Route("[controller]")]
public class ApiController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("api root");
    }
}
