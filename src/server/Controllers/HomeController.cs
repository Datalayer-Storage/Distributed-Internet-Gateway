using Microsoft.AspNetCore.Mvc;

namespace dig.server;

[ApiController]
[Route("/")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return RedirectToAction("GetWellKnown", "WellKnown");
    }
}
