using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using chia.dotnet;

namespace dig.server;

[ApiController]
[Route("/")]
public class HomeController(GatewayService gatewayService) : Controller
{
    private readonly GatewayService _gatewayService = gatewayService;

    public async Task<IActionResult> Index()
    {
        var stores = await _gatewayService.GetKnownStoresWithNames();
        return View(stores);
    }
}
