using System.Reflection;
using chia.dotnet;
using System.Web;

namespace dig.server;

public class GatewayService(DataLayerProxy dataLayer,
                            ChiaService chiaService,
                            ChiaConfig chiaConfig,
                            StoreRegistryService storeRegistryService,
                            CacheService cacheService,
                            ILogger<GatewayService> logger,
                            IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly ChiaService _chiaService = chiaService;
    private readonly StoreRegistryService _storeRegistryService = storeRegistryService;
    private readonly CacheService _cacheService = cacheService;
    private readonly ILogger<GatewayService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task<WellKnown> GetWellKnown(string baseUri, CancellationToken stoppingToken)
    {
        var xch_address = await _cacheService.GetOrCreateAsync("well_known.xch_address",
            DateTimeOffset.UtcNow + TimeSpan.FromDays(1),
            async () => await _chiaService.ResolveAddress(_configuration.GetValue("dig:XchAddress", ""), stoppingToken),
            stoppingToken
         );

        return new WellKnown(xch_address: xch_address ?? "",
                        known_stores_endpoint: $"{baseUri}/.well-known/known_stores",
                        donation_address: "xch1ctvns8zcetux57xj4hjsh5hkr40c4ascvc5uaf7gvncc3dydj9eqxenmqt", // intentionally hardcoded
                        server_version: GetAssemblyVersion());
    }

    public bool HaveDataLayerConfig() => _chiaConfig.GetEndpoint("data_layer") is not null;

    private static string GetAssemblyVersion() => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

    public async Task<IEnumerable<string>> GetKnownStores(CancellationToken cancellationToken)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_configuration.GetValue("dig:RpcTimeoutSeconds", 30)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        return await _cacheService.GetOrCreateAsync("known-stores.cache",
            TimeSpan.FromMinutes(15),
            async () => await _dataLayer.Subscriptions(linkedCts.Token),
            linkedCts.Token) ?? [];
    }

    public async Task<IEnumerable<Store>> GetKnownStoresWithNames(CancellationToken cancellationToken)
    {
        var stores = await GetKnownStores(cancellationToken);

        return stores.Select(_storeRegistryService.GetStore);
    }

    public async Task<string?> GetLastRoot(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            var rootHistory = await _cacheService.GetOrCreateAsync($"{storeId}-root-history",
                TimeSpan.FromSeconds(30),
                async () =>
                {
                    _logger.LogInformation("Getting root history for {StoreId}", storeId.SanitizeForLog());
                    return await _dataLayer.GetRootHistory(storeId, cancellationToken);
                },
                cancellationToken);

            var lastRootHistoryItem = rootHistory?.LastOrDefault();
            if (lastRootHistoryItem is not null)
            {
                return await _cacheService.GetOrCreateAsync($"root_hash_{storeId}",
                TimeSpan.FromMinutes(15),
                async () =>
                {
                    await Task.CompletedTask;
                    return lastRootHistoryItem.RootHash;
                },
                cancellationToken);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get last root for {StoreId}", storeId.SanitizeForLog());
        }

        return null;
    }

    public async Task<IEnumerable<string>?> GetKeys(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            // memory cache is used to cache the keys for 15 minutes
            var keys = await _cacheService.GetOrCreateAsync($"{storeId}-keys",
                TimeSpan.FromMinutes(15),
                async () =>
                {
                    _logger.LogInformation("Getting keys for {StoreId}", storeId.SanitizeForLog());
                    return await _dataLayer.GetKeys(storeId, null, cancellationToken);
                },
                cancellationToken);

            return keys;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get keys for {StoreId}", storeId.SanitizeForLog());
        }

        return null;  // 404 in the api
    }

    public async Task<string?> GetProof(string storeId, string hexKey, CancellationToken cancellationToken)
    {
        try
        {
            var proof = await _cacheService.GetOrCreateAsync($"{storeId}-{hexKey}-proof",
                TimeSpan.FromMinutes(15),
                async () =>
                {
                    _logger.LogInformation("Getting proof for {StoreId} {Key}", storeId.SanitizeForLog(), hexKey.SanitizeForLog());
                    var proofResponse = await _dataLayer.GetProof(storeId, [HttpUtility.UrlDecode(hexKey)], cancellationToken);
                    return proofResponse.ToJson();
                },
                cancellationToken);

            return proof;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get proof for {StoreId}", storeId.SanitizeForLog());
        }

        return null; // 404 in the api
    }

    public async Task<string?> GetValue(string storeId, string key, string? rootHash, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _cacheService.GetOrCreateAsync($"{storeId}-{key}",
                TimeSpan.FromMinutes(15),
                async () =>
                {
                    _logger.LogInformation("Getting value for {StoreId} {Key}", storeId.SanitizeForLog(), key.SanitizeForLog());
                    return await _dataLayer.GetValue(storeId, HttpUtility.UrlDecode(key), rootHash, cancellationToken);
                },
                cancellationToken);

            return value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get value for {StoreId} {Key}", storeId.SanitizeForLog(), key.SanitizeForLog());
        }

        return null; // 404 in the api
    }

    public async Task<string?> GetValueAsHtml(string storeId, string? lastStoreRootHash, CancellationToken cancellationToken)
    {
        var hexKey = HexUtils.ToHex("index.html");
        var value = await GetValue(storeId, hexKey, lastStoreRootHash, cancellationToken);
        if (value is null)
        {
            return null; // 404 in the api
        }

        var decodedValue = HexUtils.FromHex(value);
        storeId = System.Net.WebUtility.HtmlEncode(storeId); // just in case
        var baseTag = $"<base href=\"/{storeId}/\">"; // Add the base tag

        return decodedValue.Replace("<head>", $"<head>\n    {baseTag}");
    }

    public async Task<byte[]> GetValuesAsBytes(string storeId, dynamic json, string rootHash, CancellationToken cancellationToken)
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

            return await GetValue(storeId, hexKey, rootHash, cancellationToken);
        });

        var dataLayerResponses = await Task.WhenAll(hexPartsPromises);
        var resultHex = string.Join("", dataLayerResponses);

        return Convert.FromHexString(resultHex);
    }
}
