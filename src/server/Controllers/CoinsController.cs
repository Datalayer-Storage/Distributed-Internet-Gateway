using Microsoft.AspNetCore.Mvc;

namespace dig.server;

[ApiController]
[Route("api/[controller]")]
public class CoinsController(IServerCoinService serverCoinService, ILogger<CoinsController> logger) : ControllerBase
{
    private readonly IServerCoinService _serverCoinService = serverCoinService;
    private readonly ILogger<CoinsController> _logger = logger;

    [HttpGet("{storeId}")]
    public async Task<ActionResult> Get(string storeId, CancellationToken token)
    {
        if (storeId is null || storeId.Length != 64)
        {
            return NotFound();
        }

        try
        {
            // using async pattern to avoid io blocking
            var result = await _serverCoinService.GetCoins(storeId, token);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get coins for store {storeId}", storeId.SanitizeForLog());
            return NotFound();
        }
    }
}
