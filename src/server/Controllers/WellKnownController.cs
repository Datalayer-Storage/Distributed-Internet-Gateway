using Microsoft.AspNetCore.Mvc;

internal class WellKnownController(G2To3Service g2To3Service) : ControllerBase
{
    private readonly G2To3Service _g2To3Service = g2To3Service;

    [HttpGet(".well-known")]
    public IActionResult GetWellKnown()
    {
        return Ok(_g2To3Service.GetWellKnown());
    }
}
