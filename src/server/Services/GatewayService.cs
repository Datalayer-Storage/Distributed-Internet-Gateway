using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using chia.dotnet;
using System.Web;

namespace dig.server;

public sealed class GatewayService(DataLayerProxy dataLayer,
                                    ChiaConfig chiaConfig,
                                    StoreRegistryService storeRegistryService,
                                    IMemoryCache memoryCache,
                                    ILogger<GatewayService> logger,
                                    IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly StoreRegistryService _storeRegistryService = storeRegistryService;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<GatewayService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public WellKnown GetWellKnown(string baseUri) => new(xch_address: _configuration.GetValue("dig:XchAddress", "")!,
                                                                  known_stores_endpoint: $"{baseUri}/.well-known/known_stores",
                                                                  donation_address: _configuration.GetValue("dig:DonationAddress", "")!,
                                                                  server_version: GetAssemblyVersion());

    public bool HaveDataLayerConfig() => _chiaConfig.GetEndpoint("data_layer") is not null;

    private static string GetAssemblyVersion() => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

    public async Task<IEnumerable<string>> GetKnownStores()
    {
        return await _memoryCache.GetOrCreateAsync("known-stores.cache", async entry =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_configuration.GetValue("dig:RpcTimeoutSeconds", 30)));
            entry.SlidingExpiration = TimeSpan.FromMinutes(15);

            return await _dataLayer.Subscriptions(cts.Token);
        }) ?? [];
    }

    public async Task<IEnumerable<Store>> GetKnownStoresWithNames()
    {
        var stores = await GetKnownStores();

        return stores.Select(_storeRegistryService.GetStore);
    }

    public async Task<IEnumerable<string>?> GetKeys(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            // memory cache is used to cache the keys for 15 minutes
            var keys = await _memoryCache.GetOrCreateAsync(storeId, async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(15);
                _logger.LogInformation("Getting keys for {StoreId}", storeId.SanitizeForLog());

                return await _dataLayer.GetKeys(storeId, null, cancellationToken);
            });

            return keys;
        }
        catch (Exception)
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

                return await _dataLayer.GetValue(storeId, HttpUtility.UrlDecode(key), null, cancellationToken);
            });

            return value;
        }
        catch
        {
            return null; // 404 in the api
        }
    }

    public async Task<string?> GetValueAsHtml(string storeId, CancellationToken cancellationToken)
    {
        var hexKey = HexUtils.ToHex("index.html");
        var value = await GetValue(storeId, hexKey, cancellationToken);
        if (value is null)
        {
            return null; // 404 in the api
        }

        var decodedValue = HexUtils.FromHex(value);
        storeId = System.Net.WebUtility.HtmlEncode(storeId); // just in case
        var baseTag = $"<base href=\"/{storeId}/\">"; // Add the base tag

        return decodedValue.Replace("<head>", $"<head>\n    {baseTag}");
    }

    public async Task<byte[]> GetValuesAsBytes(string storeId, dynamic json, CancellationToken cancellationToken)
    {
        var multipartFileNames = json.parts as IEnumerable<string> ?? new List<string>();
        var sortedFileNames = new List<string>(multipartFileNames);
        sortedFileNames.Sort((a, b) =>
            {
                var numberA = int.Parse(a.Split(".part")[1]);
                var numberB = int.Parse(b.Split(".part")[1]);

                return numberA.CompareTo(numberB);
            });

        var hexPartsPromises = multipartFileNames.Select(async fileName =>
        {
            var hexKey = HexUtils.ToHex(HttpUtility.UrlDecode(fileName));

            return await GetValue(storeId, hexKey, cancellationToken);
        });

        var dataLayerResponses = await Task.WhenAll(hexPartsPromises);
        var resultHex = string.Join("", dataLayerResponses);

        return Convert.FromHexString(resultHex);
    }
}
