using Microsoft.AspNetCore.Mvc;

namespace dig.server;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/")]
public class HomeController(GatewayService gatewayService) : Controller
{
    private readonly GatewayService _gatewayService = gatewayService;

    public async Task<IActionResult> Index()
    {
        var stores = await _gatewayService.GetKnownStoresWithNames();
        var request = HttpContext.Request;

        ViewBag.WellKnown = _gatewayService.GetWellKnown($"{request.Scheme}://{request.Host}{request.PathBase}");

        return View(stores.OrderBy(s => s.verified_name));
    }
}
