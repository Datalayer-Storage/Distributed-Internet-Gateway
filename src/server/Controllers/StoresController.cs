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

    [HttpHead("{storeId}")]
    public async Task<IActionResult> GetStoreMeta(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            var syncStatus = await _gatewayService.GetSyncStatus(storeId, cancellationToken);
            string? rootHashQuery = HttpContext.Request.QueryString.Value?.TrimStart('?');

            if (rootHashQuery?.StartsWith("0x") == true)
            {
                rootHashQuery = rootHashQuery.Substring(2);
            }

            if (string.IsNullOrEmpty(rootHashQuery) || rootHashQuery == "latest")
            {
                HttpContext.Response.Headers.TryAdd("X-Generation-Hash", syncStatus.RootHash);
                HttpContext.Response.Headers.TryAdd("X-Synced", (syncStatus.TargetGeneration == syncStatus.Generation).ToString());
            }
            else
            {
                HttpContext.Response.Headers.TryAdd("X-Generation-Hash", rootHashQuery);

                if (rootHashQuery.Length != 64)
                {
                    return NotFound();
                }

                var rootHistory = await _gatewayService.GetRootHistory(storeId, cancellationToken);

                if (rootHistory == null)
                {
                    return NotFound();
                }

                // Convert syncStatus.Generation from uint to int for splicing
                int generation = (int)syncStatus.Generation;

                // Splice the root history based on the current generation
                var splicedRootHistory = rootHistory.Take(generation + 1).ToList();

                // Check if the query parameter root hash is in the spliced list
                bool isSynced = splicedRootHistory.Any(r =>
                {
                    string rootHash = r.RootHash.StartsWith("0x") ? r.RootHash.Substring(2) : r.RootHash;
                    return rootHash.Equals(rootHashQuery, StringComparison.OrdinalIgnoreCase);
                });

                HttpContext.Response.Headers.TryAdd("X-Synced", isSynced.ToString());
            }

            return Ok();
        }
        catch
        {
            return NotFound();
        }
    }

    [HttpHead("{storeId}/{*catchAll}")]
    public async Task<IActionResult> GetResourceMeta(string storeId, CancellationToken cancellationToken)
    {
        return await GetStoreMeta(storeId, cancellationToken);
    }

    [HttpGet("{storeId}")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> GetStore(string storeId, CancellationToken cancellationToken)
    {

        try
        {
            storeId = storeId.TrimEnd('/');

            // A referrer indicates that the user is trying to access the store from a website
            // we want to redirect them so that the URL includes the storeId in the path
            if (HttpContext.Request.Headers.TryGetValue("Referer", out var refererValues))
            {
                var referer = refererValues.ToString();
                HttpContext.Response.Headers.TryAdd("X-Referer", referer);
                var uri = new Uri(referer);
                var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (!referer.Contains(storeId) && storeId.Length != 64)
                {
                    // Remove trailing slash from referer if it exists
                    referer = HttpContext.Request.Headers["Referer"].ToString();
                    if (!string.IsNullOrEmpty(referer) && referer.EndsWith("/"))
                    {
                        referer = referer.TrimEnd('/');
                    }

                    var redirectUrl = $"{referer}{HttpContext.Request.Path}";

                    return Redirect(redirectUrl); // 302 Temporary Redirect
                }
            }

            // default to false, if we can't get the root hash, we can't be sure if the store is synced
            HttpContext.Response.Headers.TryAdd("X-Synced", "false");

            // Requesting GetValue only from the last root hash onchain ensures that only
            // nodes that have the latest state will respond to the request
            // This helps prevent a mismatch between the state of the store and
            // the data when pulled across decentralized nodes
            string? rootHash = null;
            var query = HttpContext.Request.QueryString.Value?.TrimStart('?');
            if (!string.IsNullOrEmpty(query) && query.Length == 64)
            {
                rootHash = query;
            }

            // If rootHash query parameter is not provided, get the last root hash
            if (string.IsNullOrEmpty(rootHash) || rootHash == "latest")
            {
                rootHash = await _gatewayService.GetLastRoot(storeId, cancellationToken);
                if (rootHash is null)
                {
                    return NotFound();
                }
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
            if (HttpContext.Request.Headers.TryGetValue("Referer", out var refererValues))
            {
                var referer = refererValues.ToString();
                HttpContext.Response.Headers.TryAdd("X-Referer", referer);
                var uri = new Uri(referer);
                var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (!referer.Contains(storeId) && storeId.Length != 64)
                {
                    // Remove trailing slash from referer if it exists
                    referer = HttpContext.Request.Headers["Referer"].ToString();
                    if (!string.IsNullOrEmpty(referer) && referer.EndsWith("/"))
                    {
                        referer = referer.TrimEnd('/');
                    }

                    var redirectUrl = $"{referer}{HttpContext.Request.Path}";

                    return Redirect(redirectUrl); // 302 Temporary Redirect
                }
            }

            // Requesting GetValue only from the last root hash onchain ensures that only
            // nodes that have the latest state will respond to the request
            // This helps prevent a mismatch between the state of the store and
            // the data when pulled across decentralized nodes
            string? rootHash = null;
            var query = HttpContext.Request.QueryString.Value?.TrimStart('?');
            if (!string.IsNullOrEmpty(query) && query.Length == 64)
            {
                rootHash = query;
            }

            // If rootHash query parameter is not provided, get the last root hash
            if (string.IsNullOrEmpty(rootHash) || rootHash == "latest")
            {
                rootHash = await _gatewayService.GetLastRoot(storeId, cancellationToken);
                if (rootHash is null)
                {
                    return NotFound();
                }
            }

            // info.html is a synthetic key that we use to display the store's contents
            // even though index.html returns the same, if the store overrides index.html
            // the user can still get a list of keys at info.html
            if (key == "info.html")
            {
                var keys = await _gatewayService.GetKeys(storeId, rootHash, cancellationToken);
                if (keys is not null)
                {
                    return View("StoreIndex", keys.Select(HexUtils.FromHex));
                }
            }

            // support requesting keys by hex or utf8
            var hexKey = key.StartsWith("0x") ? key : HexUtils.ToHex(key);
            var proof = await _gatewayService.GetProof(storeId, rootHash, hexKey, cancellationToken);
            if (proof is not null)
            {
                HttpContext.Response.Headers.TryAdd("X-Proof-of-Inclusion", Convert.ToBase64String(proof));
            }

            HttpContext.Response.Headers.TryAdd("X-Generation-Hash", rootHash);

            var fileExtension = Path.GetExtension(key);

            var rawValue = await _gatewayService.GetValue(storeId, hexKey, rootHash, cancellationToken);
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
                    var bytes = await _gatewayService.GetValuesAsBytes(storeId, json, rootHash, cancellationToken);

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
