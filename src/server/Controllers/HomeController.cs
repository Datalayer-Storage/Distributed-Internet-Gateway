using Microsoft.AspNetCore.Mvc;


internal class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return RedirectToAction("GetWellKnown", "WellKnown");
    }
}
