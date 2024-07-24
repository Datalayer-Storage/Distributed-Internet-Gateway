using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.Text.RegularExpressions;

namespace dig.server;

public partial class StoresController(GatewayService gatewayService,
                                        MeshNetworkRoutingService meshNetworkRoutingService,
                                        IViewEngine viewEngine,
                                        ILogger<StoresController> logger,
                                        IConfiguration configuration) : Controller
{
    private readonly GatewayService _gatewayService = gatewayService;
    private readonly MeshNetworkRoutingService _meshNetworkRoutingService = meshNetworkRoutingService;
    private readonly IViewEngine _viewEngine = viewEngine;
    private readonly ILogger<StoresController> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    [HttpHead]
    public IActionResult MyAction()
    {
        return NoContent(); // Respond with 204 No Content status code
    }

    [HttpGet("{storeId}")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> GetStore(string storeId, CancellationToken cancellationToken)
    {
        if (storeId is null || storeId.Length != 64)
        {
            return NotFound();
        }

        try
        {
            storeId = storeId.TrimEnd('/');

            // default to false, if we can't get the root hash, we can't be sure if the store is synced
            HttpContext.Response.Headers.TryAdd("X-Synced", "false");

            // A referrer indicates that the user is trying to access the store from a website
            // we want to redirect them so that the URL includes the storeId in the path
            var referer = HttpContext.Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains(storeId))
            {
                var uri = new Uri(referer);
                var host = $"{uri.Scheme}://{uri.Host}";
                if (!uri.IsDefaultPort)
                {
                    host += $":{uri.Port}";
                }
                var redirectUrl = $"{host}/{storeId}";
                HttpContext.Response.Headers["Location"] = redirectUrl;
                return Redirect(redirectUrl);
            }

            // Requesting GetValue only from the last root hash onchain ensures that only
            // nodes that have the latest state will respond to the request
            // This helps prevent a mismatch between the state of the store and
            // the data when pulled across decentralized nodes
            var rootHash = await _gatewayService.GetLastRoot(storeId, cancellationToken);
            if (rootHash is null)
            {
                return NotFound();
            }

            HttpContext.Response.Headers.TryAdd("X-Generation-Hash", rootHash);

            var keys = await _gatewayService.GetKeys(storeId, rootHash, cancellationToken);

            if (keys is not null)
            {
                var decodedKeys = keys.Select(HexUtils.FromHex).ToList();

                // the key represents a SPA app, so we want to return the index.html
                if (decodedKeys != null && decodedKeys.Count > 0 && decodedKeys.Contains("index.html"))
                {
                    HttpContext.Response.Headers.TryAdd("X-Generation-Hash", rootHash);
                    var html = await _gatewayService.GetValueAsHtml(storeId, rootHash, cancellationToken);
                    if (html is not null)
                    {

                        var proof = await _gatewayService.GetProof(storeId, rootHash, HexUtils.ToHex("index.html"), cancellationToken);
                        if (proof is not null)
                        {
                            HttpContext.Response.Headers.TryAdd("X-Proof-of-Inclusion", Convert.ToBase64String(proof));
                        }

                        // this is the case where the store is a SPA app
                        // so it should return the index.html
                        HttpContext.Response.Headers.TryAdd("X-Synced", "true");
                        return Content(html, "text/html");
                    }

                    // could not get the root hash nor the html for this store

                    return NotFound();


                }
                // return Content(IndexRenderer.Render(storeId, decodedKeys ?? []), "text/html");
                // in this case there is no index.html so we want to return the list of keys
                HttpContext.Response.Headers.TryAdd("X-Synced", "true");
                return View("StoreIndex", new StoreIndex(_gatewayService.GetStore(storeId), decodedKeys ?? []));
            }

            var actAsCdn = _configuration.GetValue<bool>("dig:ActAsCdn");

            if (actAsCdn)
            {
                var content = await _meshNetworkRoutingService.GetMeshNetworkContentsAsync(storeId, null);
                if (content is not null)
                {
                    return Content(content, "text/html");
                }
            }
            else
            {
                var redirect = await _meshNetworkRoutingService.GetMeshNetworkLocationAsync(storeId, null);
                if (redirect is not null)
                {
                    _logger.LogInformation("Redirecting to {redirect}", redirect.SanitizeForLog());
                    HttpContext.Response.Headers.Location = redirect;
                    return Redirect(redirect);
                }
            }

            try
            {
                var syncStatus = await _gatewayService.GetSyncStatus(storeId, cancellationToken);
                return View("Syncing", new SyncStatus(syncStatus));
            }
            catch
            {
                // could not get the root hash nor the html for this store
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("{storeId}/{*catchAll}")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> GetStoreCatchAll(string storeId, string catchAll, CancellationToken cancellationToken)
    {
        if (storeId is null || storeId.Length != 64)
        {
            return NotFound();
        }

        try
        {
            var key = catchAll;
            // Remove everything after the first '#'
            if (key.Contains('#'))
            {
                key = key.Split('#')[0];
            }
            key = key.TrimEnd('/');

            // A referrer indicates that the user is trying to access the store from a website
            // we want to redirect them so that the URL includes the storeId in the path
            var referer = HttpContext.Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && !referer.Contains(storeId))
            {
                key = key.TrimStart('/');
                var uri = new Uri(referer);
                var host = $"{uri.Scheme}://{uri.Host}";
                if (!uri.IsDefaultPort)
                {
                    host += $":{uri.Port}";
                }
                var redirectUrl = $"{host}/{storeId}/{key}";
                HttpContext.Response.Headers["Location"] = redirectUrl;
                return Redirect(redirectUrl);
            }

            // Requesting GetValue only from the last root hash onchain ensures that only
            // nodes that have the latest state will respond to the request
            // This helps prevent a mismatch between the state of the store and
            // the data when pulled across decentralized nodes
            var lastRootHash = await _gatewayService.GetLastRoot(storeId, cancellationToken);
            if (lastRootHash is null)
            {
                return NotFound();
            }

            // info.html is a synthetic key that we use to display the store's contents
            // even though index.html returns the same, if the store overrides index.html
            // the user can still get a list of keys at info.html
            if (key == "info.html")
            {
                var keys = await _gatewayService.GetKeys(storeId, lastRootHash, cancellationToken);
                if (keys is not null)
                {
                    return View("StoreIndex", keys.Select(HexUtils.FromHex));
                }
            }

            // support requesting keys by hex or utf8
            var hexKey = key.StartsWith("0x") ? key : HexUtils.ToHex(key);
            var proof = await _gatewayService.GetProof(storeId, lastRootHash, hexKey, cancellationToken);
            if (proof is not null)
            {
                HttpContext.Response.Headers.TryAdd("X-Proof-of-Inclusion", Convert.ToBase64String(proof));
            }

            HttpContext.Response.Headers.TryAdd("X-Generation-Hash", lastRootHash);

            var fileExtension = Path.GetExtension(key);

            var rawValue = await _gatewayService.GetValue(storeId, hexKey, lastRootHash, cancellationToken);
            if (rawValue is null)
            {
                _logger.LogInformation("couldn't find: {key}", key.SanitizeForLog());

                var actAsCdn = _configuration.GetValue<bool>("dig:ActAsCdn");

                if (actAsCdn)
                {
                    var content = await _meshNetworkRoutingService.GetMeshNetworkContentsAsync(storeId, key);
                    if (content is not null)
                    {
                        var mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";
                        return Content(content, mimeType);
                    }
                }
                else
                {
                    var redirect = await _meshNetworkRoutingService.GetMeshNetworkLocationAsync(storeId, key);
                    if (redirect is not null)
                    {
                        _logger.LogInformation("Redirecting to {redirect}", redirect.SanitizeForLog());
                        HttpContext.Response.Headers.Location = redirect;

                        return Redirect(redirect);
                    }
                }

                return NotFound();
            }

            var decodedValue = HexUtils.FromHex(rawValue);

            if (Utils.TryParseJson(decodedValue, out var json))
            {
                var expando = json as IDictionary<string, object>;
                if (expando is not null && expando.TryGetValue("type", out var type) && type?.ToString() == "multipart")
                {
                    var mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";
                    var bytes = await _gatewayService.GetValuesAsBytes(storeId, json, lastRootHash, cancellationToken);

                    return Results.File(bytes, mimeType);
                }
            }

            if (!string.IsNullOrEmpty(fileExtension))
            {
                var viewName = fileExtension.TrimStart('.');
                if (ViewExists(viewName))
                {
                    return View(viewName, decodedValue);
                }

                var mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";
                return File(Convert.FromHexString(rawValue), mimeType);
            }

            if (json is not null)
            {
                return Results.Ok(json);
            }

            if (Utils.IsBase64Image(decodedValue))
            {
                // figure out the mime type
                var regex = MimeTypeRegex();
                var match = regex.Match(decodedValue);

                // convert the base64 string to a byte array
                var base64Image = decodedValue.Split(";base64,")[^1];
                var imageBuffer = Convert.FromBase64String(base64Image);

                return File(imageBuffer, match.Value);
            }

            return Content(decodedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);

            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static string? GetMimeType(string ext)
    {
        if (MimeTypes.TryGetMimeType(ext, out var mimeType))
        {
            return mimeType;
        }

        if (otherMimeTypes.TryGetValue(ext, out var othermimeType))
        {
            return othermimeType;
        }

        return null;
    }
    private bool ViewExists(string name)
    {
        var result = _viewEngine.GetView("~/", $"~/{name}", isMainPage: false);
        return result.Success || _viewEngine.FindView(ControllerContext, name, isMainPage: false).Success;
    }
    private static readonly Dictionary<string, string> otherMimeTypes = new()
    {
        { "offer", "text/html" }
    };

    [GeneratedRegex(@"[^:]\w+\/[\w-+\d.]+(?=;|,)")]
    private static partial Regex MimeTypeRegex();
}
