using Microsoft.AspNetCore.Mvc;

namespace dig.server;

public class ErrorController(GatewayService gatewayService) : Controller
{
    private readonly GatewayService _gatewayService = gatewayService;

    [HttpGet("/error")]
    public IActionResult Error(int? statusCode = null)
    {
        var request = HttpContext.Request;
        ViewBag.WellKnown = _gatewayService.GetWellKnown($"{request.Scheme}://{request.Host}{request.PathBase}");
        if (statusCode.HasValue)
        {
            // here is the trick
            this.HttpContext.Response.StatusCode = statusCode.Value;
        }

        // or return View
        return View("Error");
    }
}
