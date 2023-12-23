using Microsoft.AspNetCore.Mvc;
namespace dig.server;

public class WellKnownController(GatewayService gatewayService) : ControllerBase
{
    private readonly GatewayService _gatewayService = gatewayService;

    [HttpGet(".well-known")]
    public ActionResult<WellKnown> Get()
    {
        var request = HttpContext.Request;
        var host = request.Host;
        var protocol = request.Scheme;
        var pathBase = request.PathBase;
        var result = _gatewayService.GetWellKnown($"{protocol}://{host}{pathBase}");

        return Ok(result);
    }

    [HttpGet(".well-known/mirrors")]
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
