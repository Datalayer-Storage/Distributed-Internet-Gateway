using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.Text.RegularExpressions;

namespace dig.server;

[MiddlewareFilter(typeof(StoreRedirectMiddlewarePipeline))]
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
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    public async Task<IActionResult> GetStoreMeta(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            var (extractedStoreId, rootHashQuery) = await ExtractStoreIdAndRootHashAsync(storeId, cancellationToken);
            
            if (string.IsNullOrEmpty(extractedStoreId) || string.IsNullOrEmpty(rootHashQuery))
            {
                return NotFound();
            }

            var headersResult = await GenerateStoreHeadersAsync(extractedStoreId, rootHashQuery, cancellationToken);

            if (!headersResult)
            {
                return NotFound();
            }

            HttpContext.Response.Headers.TryGetValue("X-Synced", out var isSynced);
            if (isSynced == "True")
            {
                return Ok();
            }
            else
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            HttpContext.Response.Headers.TryAdd("X-Synced", false.ToString());
            HttpContext.Response.Headers.TryAdd("X-Error", ex.Message.ToString());
            return NotFound();
        }
    }

    [HttpHead("{storeId}/{*catchAll}")]
    public async Task<IActionResult> GetResourceMeta(string storeId, string catchAll, CancellationToken cancellationToken)
    {
        try
        {
            var (extractedStoreId, rootHashQuery) = await ExtractStoreIdAndRootHashAsync(storeId, cancellationToken);

            if (string.IsNullOrEmpty(extractedStoreId) || string.IsNullOrEmpty(rootHashQuery))
            {
                return NotFound();
            }

            var headersResult = await GenerateStoreHeadersAsync(extractedStoreId, rootHashQuery, cancellationToken);

            if (!headersResult)
            {
                return NotFound();
            }

            if (HttpContext.Response.Headers.TryGetValue("X-Generation-Hash", out var rootHash) && !string.IsNullOrEmpty(rootHash))
            {
                var keys = await _gatewayService.GetKeys(storeId, rootHash, cancellationToken);
                if (keys != null)
                {
                    var keyExists = keys.Select(HexUtils.FromHex).Contains(catchAll);
                    HttpContext.Response.Headers.TryAdd("X-Key-Exists", keyExists.ToString());
                }
            }

            HttpContext.Response.Headers.TryGetValue("X-Synced", out var isSynced);
            if (isSynced == "True")
            {
                return Ok();
            }
            else
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            HttpContext.Response.Headers.TryAdd("X-Synced", false.ToString());
            HttpContext.Response.Headers.TryAdd("X-Error", ex.Message.ToString());
            return NotFound();
        }
    }

    [HttpGet("{storeId}")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> GetStore(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            var (extractedStoreId, rootHash) = await ExtractStoreIdAndRootHashAsync(storeId, cancellationToken);

            HttpContext.Response.Headers.TryAdd("X-Generation-Hash", rootHash);

            var keys = await _gatewayService.GetKeys(extractedStoreId, rootHash, cancellationToken);
            bool isSynced = false;

            if (keys is not null)
            {
                var decodedKeys = keys.Select(HexUtils.FromHex).ToList();

                if (decodedKeys != null && decodedKeys.Count > 0 && decodedKeys.Contains("index.html"))
                {
                    var html = await _gatewayService.GetValueAsHtml(extractedStoreId, rootHash, cancellationToken);
                    if (html is not null)
                    {
                        var proof = await _gatewayService.GetProof(extractedStoreId, rootHash, HexUtils.ToHex("index.html"), cancellationToken);
                        if (proof is not null)
                        {
                            HttpContext.Response.Headers.TryAdd("X-Proof-of-Inclusion", Convert.ToBase64String(proof));
                        }

                        isSynced = true;
                        HttpContext.Response.Headers.TryAdd("X-Synced", isSynced.ToString());
                        return Content(html, "text/html");
                    }

                    return NotFound();
                }

                isSynced = true;
                HttpContext.Response.Headers.TryAdd("X-Synced", isSynced.ToString());
                return View("StoreIndex", new StoreIndex(_gatewayService.GetStore(extractedStoreId), decodedKeys ?? []));
            }

            var actAsCdn = _configuration.GetValue<bool>("dig:ActAsCdn");

            var redirect = await _meshNetworkRoutingService.GetMeshNetworkLocationAsync(extractedStoreId, null);
            if (redirect is not null)
            {
                _logger.LogInformation("Redirecting to {redirect}", redirect.SanitizeForLog());
                HttpContext.Response.Headers.Location = redirect;
                return Redirect(redirect);
            }
          
            try
            {
                var syncStatus = await _gatewayService.GetSyncStatus(extractedStoreId, cancellationToken);
                isSynced = syncStatus.TargetGeneration == syncStatus.Generation;
                HttpContext.Response.Headers.TryAdd("X-Synced", isSynced.ToString());
                return View("Syncing", new SyncStatus(syncStatus));
            }
            catch
            {
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
            var (extractedStoreId, rootHash) =  await ExtractStoreIdAndRootHashAsync(storeId, cancellationToken);

            var key = catchAll;
            // Remove everything after the first '#'
            if (key.Contains('#'))
            {
                key = key.Split('#')[0];
            }
            key = key.TrimEnd('/');

            HttpContext.Response.Headers.TryAdd("X-Generation-Hash", rootHash);

            // info.html is a synthetic key that we use to display the store's contents
            if (key == "info.html")
            {
                var keys = await _gatewayService.GetKeys(extractedStoreId, rootHash, cancellationToken);
                if (keys is not null)
                {
                    return View("StoreIndex", keys.Select(HexUtils.FromHex));
                }
            }

            var hexKey = key.StartsWith("0x") ? key : HexUtils.ToHex(key);
            var proof = await _gatewayService.GetProof(extractedStoreId, rootHash, hexKey, cancellationToken);
            if (proof is not null)
            {
                HttpContext.Response.Headers.TryAdd("X-Proof-of-Inclusion", Convert.ToBase64String(proof));
            }

            var fileExtension = Path.GetExtension(key);

            var rawValue = await _gatewayService.GetValue(extractedStoreId, hexKey, rootHash, cancellationToken);
            if (rawValue is null)
            {
                _logger.LogInformation("couldn't find: {key}", key.SanitizeForLog());

                var actAsCdn = _configuration.GetValue<bool>("dig:ActAsCdn");

                if (actAsCdn)
                {
                    var content = await _meshNetworkRoutingService.GetMeshNetworkContentsAsync(extractedStoreId, key);
                    if (content is not null)
                    {
                        var mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";
                        return Content(content, mimeType);
                    }
                }
                else
                {
                    var redirect = await _meshNetworkRoutingService.GetMeshNetworkLocationAsync(extractedStoreId, key);
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
                    var bytes = await _gatewayService.GetValuesAsBytes(extractedStoreId, json, rootHash, cancellationToken);

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
                var regex = MimeTypeRegex();
                var match = regex.Match(decodedValue);

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

    private async Task<(string? StoreId, string? RootHash)> ExtractStoreIdAndRootHashAsync(string input, CancellationToken cancellationToken)
    {
        // int StoreIdLength = 64;
        input = input.TrimEnd('/').TrimStart('/');
        input = input.Contains("%24") ? Uri.UnescapeDataString(input) : input;

        int atIndex = input.IndexOf('$');
        string storeId = atIndex == -1 ? input : input.Substring(0, atIndex);
        string? rootHash = atIndex == -1 ? "latest" : input.Substring(atIndex + 1);

        if (rootHash.Equals("latest"))
        {
            rootHash = await _gatewayService.GetLastRoot(storeId, cancellationToken);
            if (rootHash is null)
            {
                return (null, null);
            }
        }

        return (storeId, rootHash.StartsWith("0x") ? rootHash.Substring(2) : rootHash);
    }

    private async Task<bool> GenerateStoreHeadersAsync(string storeId, string rootHashQuery, CancellationToken cancellationToken)
    {
        var syncStatus = await _gatewayService.GetSyncStatus(storeId, cancellationToken);

        HttpContext.Response.Headers.TryAdd("X-Generation-Hash", rootHashQuery);

        var rootHistory = await _gatewayService.GetRootHistory(storeId, cancellationToken);

        if (rootHistory == null)
        {
            return false;
        }

        int generation = (int)syncStatus.Generation;
        var splicedRootHistory = rootHistory.Take(generation + 1).ToList();

        bool isSynced = splicedRootHistory.Any(r =>
        {
            string rootHash = r.RootHash.StartsWith("0x") ? r.RootHash.Substring(2) : r.RootHash;
            return rootHash.Equals(rootHashQuery, StringComparison.OrdinalIgnoreCase);
        });

        HttpContext.Response.Headers.TryAdd("X-Synced", isSynced.ToString());

        return true;
    }
}
