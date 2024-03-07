using Microsoft.AspNetCore.Mvc;

namespace dig.server;

public class ErrorController(GatewayService gatewayService) : Controller
{
    private readonly GatewayService _gatewayService = gatewayService;

    [HttpGet("/error")]
    public async Task<IActionResult> ErrorAsync(int? statusCode = null, CancellationToken cancellationToken = default)
    {
        var request = HttpContext.Request;
        ViewBag.WellKnown = await _gatewayService.GetWellKnown($"{request.Scheme}://{request.Host}{request.PathBase}", cancellationToken);
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
