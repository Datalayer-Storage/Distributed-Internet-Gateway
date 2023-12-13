using Microsoft.AspNetCore.Mvc;
namespace dig.server;

public class WellKnownController(GatewayService g2To3Service) : ControllerBase
{
    private readonly GatewayService _g2To3Service = g2To3Service;

    [HttpGet(".well-known")]
    public IActionResult GetWellKnown()
    {
        return Ok(_g2To3Service.GetWellKnown());
    }
}
