using Microsoft.AspNetCore.Mvc;

namespace dig.server;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/")]
public class HomeController(GatewayService gatewayService) : Controller
{
    private readonly GatewayService _gatewayService = gatewayService;

    public async Task<IActionResult> Index()
    {
        var request = HttpContext.Request;
        ViewBag.WellKnown = _gatewayService.GetWellKnown($"{request.Scheme}://{request.Host}{request.PathBase}");

        if (_gatewayService.HaveDataLayerConfig())
        {
            var stores = await _gatewayService.GetKnownStoresWithNames();
            return View(stores.OrderByDescending(s => s.is_verified).ThenBy(s => s.verified_name));
        }

        return View(Enumerable.Empty<Store>());
    }
}
