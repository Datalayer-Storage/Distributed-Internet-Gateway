namespace dig.server;
public class StoreRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public StoreRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, GatewayService gatewayService)
    {
        var path = context.Request.Path.Value?.Trim('/');
        if (string.IsNullOrEmpty(path))
        {
            await _next(context);
            return;
        }

        var pathSegments = path.Split('/');
        var maybeStoreId = pathSegments[0];
        var restOfPath = string.Join("/", pathSegments.Skip(1));

        if (maybeStoreId.Length.Equals(129))
        {
            // This is a valid storeId + rootHash
            await _next(context); // Proceed to the next middleware
        }
        else if (maybeStoreId.Length.Equals(64) || maybeStoreId.Length.Equals(71))
        {
            // This is a valid storeId but it needs the latest rootHash
           // var lastRoot = await gatewayService.GetLastRoot(maybeStoreId, context.RequestAborted);
           // if (lastRoot == null)
           // {
           //     context.Response.StatusCode = StatusCodes.Status404NotFound;
           //     return;
            //}
           // var rootHash = lastRoot.StartsWith("0x") ? lastRoot.Substring(2) : lastRoot;
           // var redirectUrl = $"/{maybeStoreId}${rootHash}/{restOfPath}";
           // context.Response.Redirect(redirectUrl);
           await _next(context); 
        }
        else
        {
            if (context.Request.Headers.TryGetValue("Referer", out var refererValues))
            {
                var referer = refererValues.ToString().TrimEnd('/');
                var (storeId, rootHash) = ExtractStoreIdAndRootHash(ExtractStoreIdFromReferrer(referer));
                if (String.IsNullOrEmpty(rootHash) || rootHash.Equals("latest"))
                {
                    context.Response.Redirect($"/{storeId}/{path}");
                }
                else 
                {
                    context.Response.Redirect($"/{storeId}${rootHash}/{path}");
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            }
        }
    }

    private (string? StoreId, string? RootHash) ExtractStoreIdAndRootHash(string input)
    {
        // int StoreIdLength = 64;
        input = input.TrimEnd('/').TrimStart('/');
        input = input.Contains("%24") ? Uri.UnescapeDataString(input) : input;

        int atIndex = input.IndexOf('$');
        string storeId = atIndex == -1 ? input : input.Substring(0, atIndex);
        string? rootHash = atIndex == -1 ? "latest" : input.Substring(atIndex + 1);

        return (storeId, rootHash.StartsWith("0x") ? rootHash.Substring(2) : rootHash);
    }

    private string? ExtractStoreIdFromReferrer(string referer)
    {
        var uri = new Uri(referer);
        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length > 0)
        {
            var storeId = pathSegments[0];
            return storeId;
        }
        return null;
    }
}