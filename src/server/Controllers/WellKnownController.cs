using Microsoft.AspNetCore.Mvc;

namespace dig.server;

public class WellKnownController(GatewayService gatewayService) : ControllerBase
{
    private readonly GatewayService _gatewayService = gatewayService;

    [HttpGet(".well-known")]
    public ActionResult<WellKnown> Get()
    {
        var request = HttpContext.Request;
        var wellKnown = _gatewayService.GetWellKnown($"{request.Scheme}://{request.Host}{request.PathBase}");

        return Ok(wellKnown);
    }

    [HttpGet(".well-known/known_stores")]
    public async Task<ActionResult<IEnumerable<string>>> GetMirrors()
    {
        try
        {
            var stores = await _gatewayService.GetKnownStores();
            return Ok(stores);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
