using Microsoft.AspNetCore.Mvc;

namespace dig.server;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/")]
public class HomeController(GatewayService gatewayService, ChiaService chiaService) : Controller
{
    private readonly GatewayService _gatewayService = gatewayService;
    private readonly ChiaService _chiaService = chiaService;

    public async Task<IActionResult> IndexAsync(CancellationToken cancellationToken)
    {
        if (_gatewayService.HaveDataLayerConfig())
        {
            var stores = await _gatewayService.GetKnownStoresWithNames(cancellationToken);
            var walletBalance = await _chiaService.GetNodeWalletBalance(cancellationToken);
            var nodeAddress = await _chiaService.ResolveAddress(null, cancellationToken);

            var model = new HomeViewModel
            {
                Stores = stores.OrderByDescending(s => s.is_verified).ThenBy(s => s.verified_name),
                WalletBalance = walletBalance,
                NodeAddress = nodeAddress
            };

            return View(model);
        }

        return View(new HomeViewModel
        {
            Stores = Enumerable.Empty<Store>(),
            WalletBalance = 0,
            NodeAddress = string.Empty
        });
    }

}
