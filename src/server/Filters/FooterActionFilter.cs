using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace dig.server;

public class FooterDataFilter(GatewayService gatewayService) : IAsyncActionFilter
{
    private readonly GatewayService _gatewayService = gatewayService;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // This will run before the action method is executed.
        if (context.Controller is Controller controller)
        {
            var request = context.HttpContext.Request;
            var cancellationToken = context.HttpContext.RequestAborted; // Get the CancellationToken
            controller.ViewBag.WellKnown = await _gatewayService.GetWellKnown($"{request.Scheme}://{request.Host}{request.PathBase}", cancellationToken);
        }

        // Call the next delegate in the pipeline
        await next();
    }
}
