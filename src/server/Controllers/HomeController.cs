using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using chia.dotnet;

namespace dig.server;

[ApiController]
[Route("/")]
public class HomeController(DataLayerProxy dataLayer,
                                IMemoryCache memoryCache) : ControllerBase
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly IMemoryCache _memoryCache = memoryCache;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> Get()
    {
        try
        {
            var subscriptions = await _memoryCache.GetOrCreateAsync("subscriptions.list", async entry =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                entry.SlidingExpiration = TimeSpan.FromMinutes(15);
                return await _dataLayer.Subscriptions(cts.Token);
            });

            return Ok(subscriptions);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
