using Microsoft.AspNetCore.Mvc;
using chia.dotnet;

namespace dig.server;

[ApiController]
[Route("api/[controller]")]
public class StatusController(DataLayerProxy dataLayer, ILogger<StatusController> logger) : ControllerBase
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ILogger<StatusController> _logger = logger;

    [HttpGet("{storeId}")]
    public async Task<IActionResult> GetStatusAsync(string storeId, CancellationToken cancellationToken)
    {
        if (storeId is null || storeId.Length != 64)
        {
            return NotFound();
        }

        try
        {
            var status = await _dataLayer.GetSyncStatus(storeId, cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get coins for store {storeId}", storeId.SanitizeForLog());
            return NotFound();
        }
    }
}
