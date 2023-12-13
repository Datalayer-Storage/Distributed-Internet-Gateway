using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
namespace dig.server;

public class StoreController(GatewayService gatewayService, ILogger<StoreController> logger) : ControllerBase
{
    private readonly GatewayService _gatewayService = gatewayService;
    private readonly ILogger<StoreController> _logger = logger;

    [HttpGet("{storeId}")]
    public async Task<IActionResult> GetStore(HttpContext httpContext, string storeId, bool? showKeys, CancellationToken cancellationToken)
    {
        try
        {
            storeId = storeId.TrimEnd('/');

            // A referrer indicates that the user is trying to access the store from a website
            // we want to redirect them so that the URL includes the storeId in the path
            var referer = httpContext.Request.Headers["referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains(storeId))
            {
                httpContext.Response.Headers["Location"] = $"{referer}/{storeId}";
                return Redirect($"{referer}/{storeId}");
            }

            var keys = await _gatewayService.GetKeys(storeId, cancellationToken);

            if (keys is not null)
            {
                var decodedKeys = keys.Select(HexUtils.FromHex).ToList();

                // the key represents a SPA app, so we want to return the index.html
                if (decodedKeys != null && decodedKeys.Count > 0 && decodedKeys.Contains("index.html") && showKeys != true)
                {
                    var html = await _gatewayService.GetValueAsHtml(storeId, cancellationToken);
                    return Content(html, "text/html");
                }

                return Ok();
            }

            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("{storeId}/{*catchAll}")]
    public async Task<IActionResult> GetStoreCatchAll(HttpContext httpContext, string storeId, string catchAll, CancellationToken cancellationToken)
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
            var referer = httpContext.Request.Headers["referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && !referer.Contains(storeId))
            {
                key = key.TrimStart('/');
                httpContext.Response.Headers["Location"] = $"{referer}/{storeId}/{key}";

                return Redirect($"{referer}/{storeId}/{key}");
            }

            var hexKey = HexUtils.ToHex(key);
            var rawValue = await _gatewayService.GetValue(storeId, hexKey, cancellationToken);
            if (rawValue is null)
            {
                _logger.LogInformation($"couldn't find: {key}");
                return NotFound();
            }

            var decodedValue = HexUtils.FromHex(rawValue);
            var fileExtension = Path.GetExtension(key);

            if (Utils.TryParseJson(decodedValue, out var json) && json?.type == "multipart")
            {
                string mimeType = GetMimeType(fileExtension) ?? "application/octet-stream";
                var bytes = await _gatewayService.GetValuesAsBytes(storeId, json, cancellationToken);

                return Results.File(bytes, mimeType);
            }
            else if (!string.IsNullOrEmpty(fileExtension))
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
                var regex = new Regex(@"[^:]\w+\/[\w-+\d.]+(?=;|,)");
                var match = regex.Match(decodedValue);

                // convert the base64 string to a byte array
                string base64Image = decodedValue.Split(";base64,")[^1];
                byte[] imageBuffer = Convert.FromBase64String(base64Image);

                return File(imageBuffer, match.Value);
            }
            else
            {
                return Content(decodedValue);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string? GetMimeType(string ext)
    {
        if (dig.MimeTypes.TryGetMimeType(ext, out var mimeType))
        {
            return mimeType;
        }

        return null;
    }
}
