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
            HttpContext.Response.StatusCode = statusCode.Value;
        }

        ViewBag.ErrorMessage = HttpContext.Response.StatusCode switch
        {
            404 => "That was not found",
            _ => "Eek",
        };

        return View("Error");
    }
}
