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

    [HttpGet("healthz")]
    public IActionResult Test()
    {
        return Ok(new { status = "aok" });
    }
}
