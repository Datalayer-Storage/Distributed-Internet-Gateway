using System.Text;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace dig.server;

public partial class StoresController(GatewayService gatewayService,
                                        MeshNetworkRoutingService meshNetworkRoutingService,
                                        ILogger<StoresController> logger, IConfiguration configuration) : ControllerBase
{
    private readonly GatewayService _gatewayService = gatewayService;
    private readonly ILogger<StoresController> _logger = logger;
    private readonly MeshNetworkRoutingService _meshNetworkRoutingService = meshNetworkRoutingService;
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

            // A referrer indicates that the user is trying to access the store from a website
            // we want to redirect them so that the URL includes the storeId in the path
            var referer = HttpContext.Request.Headers.Referer.ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains(storeId))
            {
                HttpContext.Response.Headers.Location = $"{referer}/{storeId}";
                return Redirect($"{referer}/{storeId}");
            }

            var keys = await _gatewayService.GetKeys(storeId, cancellationToken);

            if (keys is not null)
            {
                var decodedKeys = keys.Select(HexUtils.FromHex).ToList();

                // the key represents a SPA app, so we want to return the index.html
                if (decodedKeys != null && decodedKeys.Count > 0 && decodedKeys.Contains("index.html"))
                {
                    var lastStoreRootHash = await _gatewayService.GetLastRoot(storeId, cancellationToken);

                    if (lastStoreRootHash is not null)
                    {
                        HttpContext.Response.Headers.TryAdd("X-Generation-Hash", lastStoreRootHash);
                    }

                    var html = await _gatewayService.GetValueAsHtml(storeId, lastStoreRootHash, cancellationToken);
                    if (html is not null)
                    {
                        var disableProof = _configuration.GetValue<bool>("dig:DisableProofOfInclusion");

                        if (!disableProof)
                        {
                            var proof = await _gatewayService.GetProof(storeId, HexUtils.ToHex("index.html"), cancellationToken);
                            if (proof is not null)
                            {
                                HttpContext.Response.Headers.TryAdd("X-Proof-of-Inclusion", proof);
                            }
                        }


                        return Content(html, "text/html");
                    }
                }

                var htmlContent = IndexRenderer.Render(storeId, decodedKeys ?? []);

                return Content(htmlContent, "text/html");
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


            return NotFound();

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
            var referer = HttpContext.Request.Headers.Referer.ToString();
            if (!string.IsNullOrEmpty(referer) && !referer.Contains(storeId))
            {
                key = key.TrimStart('/');
                HttpContext.Response.Headers.Location = $"{referer}/{storeId}/{key}";

                return Redirect($"{referer}/{storeId}/{key}");
            }

            // info.html is a synthetic key that we use to display the store's contents
            // even though index.html returns the same, if the store overrides index.html
            // the user can still get a list of keys at info.html
            if (key == "info.html")
            {
                var keys = await _gatewayService.GetKeys(storeId, cancellationToken);
                if (keys is not null)
                {
                    var htmlContent = IndexRenderer.Render(storeId, keys.Select(HexUtils.FromHex));

                    return Content(htmlContent, "text/html");
                }
            }

            var hexKey = key.StartsWith("0x") ? key : HexUtils.ToHex(key);
            var disableProof = _configuration.GetValue<bool>("dig:DisableProofOfInclusion");

            if (!disableProof)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var proof = await _gatewayService.GetProof(storeId, hexKey, cancellationToken);
                _logger.LogInformation($"GetProof {proof} ms");
                stopwatch.Stop();
                _logger.LogInformation($"GetProof command completed in {stopwatch.ElapsedMilliseconds} ms");

                if (proof is not null)
                {
                    byte[] proofBytes = Encoding.UTF8.GetBytes(proof);

                    // Convert the byte array to a Base64 string
                    string proofBase64 = Convert.ToBase64String(proofBytes);
                    HttpContext.Response.Headers.TryAdd("X-Proof-of-Inclusion", proofBase64);
                    HttpContext.Response.Headers.TryAdd("X-Gen-Time", stopwatch.ElapsedMilliseconds.ToString());
                }
            }

            // Requesting GetValue only from the last root hash onchain ensures that only
            // nodes that have the latest state will respond to the request
            // This helps prevent a mismatch between the state of the store and
            // the data when pulled across decentralized nodes
            var lastStoreRootHash = await _gatewayService.GetLastRoot(storeId, cancellationToken);

            if (lastStoreRootHash is not null)
            {
                HttpContext.Response.Headers.TryAdd("X-Generation-Hash", lastStoreRootHash);
            }

            var fileExtension = Path.GetExtension(key);

            var rawValue = await _gatewayService.GetValue(storeId, hexKey, lastStoreRootHash, cancellationToken);
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
                    var bytes = await _gatewayService.GetValuesAsBytes(storeId, json, lastStoreRootHash, cancellationToken);

                    return Results.File(bytes, mimeType);
                }
            }

            if (!string.IsNullOrEmpty(fileExtension))
            {
                var renderContents = RenderFactory.Render(storeId, decodedValue, fileExtension, Request);
                var mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";

                if (renderContents is not null)
                {
                    return Content(renderContents, "text/html");
                }

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

    private static readonly Dictionary<string, string> otherMimeTypes = new()
    {
        { "offer", "text/html" }
    };

    [GeneratedRegex(@"[^:]\w+\/[\w-+\d.]+(?=;|,)")]
    private static partial Regex MimeTypeRegex();
}
