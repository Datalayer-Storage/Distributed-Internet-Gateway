using Microsoft.Extensions.Caching.Memory;

using chia.dotnet;

internal record WellKnown()
{
    public string xch_address { get; init; } = "";
    public string donation_address { get; init; } = "";
}

internal sealed class G2To3Service
{
    private readonly DataLayerProxy _dataLayer;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<G2To3Service> _logger;
    private readonly IConfiguration _configuration;

    public G2To3Service(DataLayerProxy dataLayer, IMemoryCache memoryCache, ILogger<G2To3Service> logger, IConfiguration configuration) =>
            (_dataLayer, _memoryCache, _logger, _configuration) = (dataLayer, memoryCache, logger, configuration);

    public WellKnown GetWellKnown()
    {
        return new WellKnown
        {
            xch_address = _configuration.GetValue("App:xch_address", "")!,
            donation_address = _configuration.GetValue("App:donation_address", "")!
        };
    }

    public async Task<IEnumerable<string>?> GetKeys(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            var keys = await _memoryCache.GetOrCreateAsync($"{storeId}", async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(15);
                _logger.LogInformation("Getting keys for {StoreId}", storeId.SanitizeForLog());
                return await _dataLayer.GetKeys(storeId, null, cancellationToken);
            });

            return keys;
        }
        catch
        {
            return null;  // 404 in the api
        }
    }

    public async Task<string?> GetValue(string storeId, string key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _memoryCache.GetOrCreateAsync($"{storeId}-{key}", async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(15);
                _logger.LogInformation("Getting value for {StoreId} {Key}", storeId.SanitizeForLog(), key.SanitizeForLog());
                return await _dataLayer.GetValue(storeId, key, null, cancellationToken);
            });

            return value;
        }
        catch
        {
            return null; // 404 in the api
        }
    }

    public async Task<string> GetValueAsHtml(string storeId, CancellationToken cancellationToken)
    {
        var hexKey = HexUtils.ToHex("index.html");
        var value = await GetValue(storeId, hexKey, cancellationToken) ?? throw new InvalidOperationException("Couldn't retrieve expected key value");
        var decodedValue = HexUtils.FromHex(value);
        var baseTag = $"<base href=\"/{storeId}/\">"; // Add the base tag

        return decodedValue.Replace("<head>", $"<head>\n    {baseTag}");
    }

    public async Task<byte[]> GetValuesAsBytes(string storeId, dynamic json, CancellationToken cancellationToken)
    {
        var multipartFileNames = json.parts as IEnumerable<string> ?? new List<string>();
        var sortedFileNames = new List<string>(multipartFileNames);
        sortedFileNames.Sort((a, b) =>
            {
                int numberA = int.Parse(a.Split(".part")[1]);
                int numberB = int.Parse(b.Split(".part")[1]);
                return numberA.CompareTo(numberB);
            });

        var hexPartsPromises = multipartFileNames.Select(async fileName =>
        {
            var hexKey = HexUtils.ToHex(fileName);
            return await GetValue(storeId, hexKey, cancellationToken);
        });
        var dataLayerResponses = await Task.WhenAll(hexPartsPromises);
        var resultHex = string.Join("", dataLayerResponses);

        return Convert.FromHexString(resultHex);
    }
}
