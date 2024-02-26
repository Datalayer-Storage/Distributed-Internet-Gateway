using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace dig.server;

public partial class StoresController(GatewayService gatewayService,
                                        ILogger<StoresController> logger) : ControllerBase
{
    private readonly GatewayService _gatewayService = gatewayService;
    private readonly ILogger<StoresController> _logger = logger;

    public async Task<IActionResult> GetDefaultIndexHtml(string storeId, CancellationToken cancellationToken)
    {
        var keys = await _gatewayService.GetKeys(storeId, cancellationToken);

        if (keys is not null)
        {
            var decodedKeys = keys.Select(HexUtils.FromHex).ToList();

            // the key represents a SPA app, so we want to return the index.html
            if (decodedKeys != null && decodedKeys.Count > 0 && decodedKeys.Contains("index.html"))
            {
                var html = await _gatewayService.GetValueAsHtml(storeId, cancellationToken);
                if (html is not null)
                {
                    return Content(html, "text/html");
                }

                return NotFound();
            }

            var htmlContent = $"<html><body><h1>Index of {storeId}</h1>";

            if (decodedKeys.Count > 0) {
                htmlContent += "<ul>";
                foreach (var key in decodedKeys) {
                    var link = $"{Request.Scheme}://{Request.Host}/{storeId}/{key}";
                    htmlContent += $"<li><a href='{link}'>{key}</a></li>";
                }
                htmlContent += "</ul>";
            } else {
                htmlContent += "<p>This store is empty.</p>";
            }

            htmlContent += "</body></html>";
            
            return Content(htmlContent, "text/html");
        }

        return NotFound();
    }

    [HttpGet("{storeId}")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> GetStore(string storeId, bool? showKeys, CancellationToken cancellationToken)
    {
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

            return await GetDefaultIndexHtml(storeId, cancellationToken);
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
                return await GetDefaultIndexHtml(storeId, cancellationToken);
            }

            var hexKey = HexUtils.ToHex(key);
            var rawValue = await _gatewayService.GetValue(storeId, hexKey, cancellationToken);
            if (rawValue is null)
            {
                _logger.LogInformation("couldn't find: {key}", key.SanitizeForLog());

                return NotFound();
            }

            var decodedValue = HexUtils.FromHex(rawValue);
            var fileExtension = Path.GetExtension(key);

            if (Utils.TryParseJson(decodedValue, out var json))
            {
                IDictionary<string, object>? expando = json as IDictionary<string, object>;
                if (expando is not null && expando.TryGetValue("type", out var type) && type?.ToString() == "multipart")
                {
                    string mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";
                    var bytes = await _gatewayService.GetValuesAsBytes(storeId, json, cancellationToken);

                    return Results.File(bytes, mimeType);
                }
            }

            if (!string.IsNullOrEmpty(fileExtension))
            {
                string mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";

                return File(Convert.FromHexString(rawValue), mimeType);
            }
            else if (json is not null)
            {
                return Results.Ok(json);
            }
            else if (Utils.IsBase64Image(decodedValue))
            {
                // figure out the mime type
                var regex = MimeTypeRegex();
                var match = regex.Match(decodedValue);

                // convert the base64 string to a byte array
                string base64Image = decodedValue.Split(";base64,")[^1];
                byte[] imageBuffer = Convert.FromBase64String(base64Image);

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

        return null;
    }

    [GeneratedRegex(@"[^:]\w+\/[\w-+\d.]+(?=;|,)")]
    private static partial Regex MimeTypeRegex();
}
