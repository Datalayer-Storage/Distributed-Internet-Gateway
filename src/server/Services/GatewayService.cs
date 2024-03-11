using System.Reflection;
using chia.dotnet;
using System.Web;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace dig.server;

public class GatewayService(DataLayerProxy dataLayer,
                            ChiaService chiaService,
                            ChiaConfig chiaConfig,
                            StoreRegistryService storeRegistryService,
                            FileCacheService fileCacheService,
                            IMemoryCache memoryCache,
                            ILogger<GatewayService> logger,
                            IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly ChiaService _chiaService = chiaService;
    private readonly StoreRegistryService _storeRegistryService = storeRegistryService;
    private readonly FileCacheService _fileCacheService = fileCacheService;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<GatewayService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task<WellKnown> GetWellKnown(string baseUri, CancellationToken stoppingToken)
    {
        var xch_address = await _memoryCache.GetOrCreateAsync("well_known.xch_address", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            return await _chiaService.ResolveAddress(_configuration.GetValue("dig:XchAddress", ""), stoppingToken);
        });

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
        return await _memoryCache.GetOrCreateAsync("known-stores.cache", async (entry) =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(15);
            return await _dataLayer.Subscriptions(linkedCts.Token);
        }) ?? [];
    }

    public async Task<IEnumerable<Store>> GetKnownStoresWithNames(CancellationToken cancellationToken)
    {
        var stores = await GetKnownStores(cancellationToken);

        return stores.Select(_storeRegistryService.GetStore);
    }

    public Store GetStore(string storeId) => _storeRegistryService.GetStore(storeId);

    private async Task<string> RefreshStoreRootHash(string storeId, CancellationToken cancellationToken)
    {
        var currentRoot = await _dataLayer.GetRoot(storeId, cancellationToken);
        var cachedRootHash = await _fileCacheService.GetValueAsync<RootHash>(storeId, "", "last-root", cancellationToken);

        // the current hash doesn't match the persistent cache
        if (cachedRootHash?.Hash != currentRoot.Hash)
        {
            _logger.LogWarning("Invalidating cache for {StoreId}", storeId.SanitizeForLog());
            _fileCacheService.RemoveStore(storeId);
            await _fileCacheService.SetValueAsync(storeId, "", "last-root", currentRoot, cancellationToken);
        }

        return currentRoot.Hash;
    }

    public async Task<string?> GetLastRoot(string storeId, CancellationToken cancellationToken)
    {
        try
        {
            // this will cache a stores root hash for 15 seconds in memory
            // this is to prevent a burst of activity from causing a burst of requests to the data layer
            // on each refresh the file cache will be checked to see if the root hash is still valid
            return await _memoryCache.GetOrCreateAsync($"{storeId}-last-root", async (entry) =>
            {
                entry.SlidingExpiration = TimeSpan.FromSeconds(15);
                return await RefreshStoreRootHash(storeId, cancellationToken);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get last root for {StoreId}", storeId.SanitizeForLog());
        }

        return null; // no root hash, no store - this will signal 404 upstream
    }

    public async Task<IEnumerable<string>?> GetKeys(string storeId, string rootHash, CancellationToken cancellationToken)
    {
        try
        {
            var keys = await _fileCacheService.GetOrCreateAsync(storeId, rootHash, "keys",
                async () =>
                {
                    _logger.LogInformation("Getting keys for {StoreId}", storeId.SanitizeForLog());
                    return await _dataLayer.GetKeys(storeId, rootHash, cancellationToken);
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

    public async Task<byte[]?> GetProof(string storeId, string rootHash, string key, CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("dig:DisableProofOfInclusion", true))
        {
            try
            {
                var proof = await _fileCacheService.GetOrCreateAsync(storeId, rootHash, $"{key}-proof",
                    async () =>
                    {
                        _logger.LogInformation("Getting proof for {StoreId} {Key}", storeId.SanitizeForLog(), key.SanitizeForLog());
                        var proof = await _dataLayer.GetProof(storeId, [HttpUtility.UrlDecode(key)], cancellationToken);
                        return proof.ToJson();
                    },
                    cancellationToken);

                return proof is not null ? Encoding.UTF8.GetBytes(proof) : null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get proof for {StoreId}", storeId.SanitizeForLog());
            }
        }

        return null;
    }

    public async Task<string?> GetValue(string storeId, string key, string rootHash, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _fileCacheService.GetOrCreateAsync(storeId, rootHash, key,
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

    public async Task<string?> GetValueAsHtml(string storeId, string rootHash, CancellationToken cancellationToken)
    {
        var hexKey = HexUtils.ToHex("index.html");
        var value = await GetValue(storeId, hexKey, rootHash, cancellationToken);
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
