using Microsoft.AspNetCore.Mvc;

namespace dig.server;

[ApiController]
[Route("api/[controller]")]
public class CoinsController(ServerCoinService serverCoinService, ILogger<CoinsController> logger) : ControllerBase
{
    private readonly ServerCoinService _serverCoinService = serverCoinService;
    private readonly ILogger<CoinsController> _logger = logger;

    [HttpGet("{storeId}")]
    public async Task<ActionResult> Get(string storeId)
    {
        try
        {
            // using async pattern to avoid io blocking
            await Task.CompletedTask;
            return Ok(_serverCoinService.GetCoins(storeId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get coins for store {storeId}", storeId.SanitizeForLog());
            return NotFound();
        }
    }
}
