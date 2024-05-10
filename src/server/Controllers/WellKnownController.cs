using Microsoft.AspNetCore.Mvc;

namespace dig.server;

public class WellKnownController(GatewayService gatewayService) : ControllerBase
{
    private readonly GatewayService _gatewayService = gatewayService;

    [HttpGet(".well-known")]
    public async Task<ActionResult<WellKnown>> GetAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.Request;
        var wellKnown = await _gatewayService.GetWellKnown($"{request.Scheme}://{request.Host}{request.PathBase}", cancellationToken);

        return Ok(wellKnown);
    }

    [HttpGet(".well-known/known_stores")]
    public async Task<ActionResult<IEnumerable<string>>> GetMirrorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var stores = await _gatewayService.GetKnownStores(cancellationToken);
            return Ok(stores);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
