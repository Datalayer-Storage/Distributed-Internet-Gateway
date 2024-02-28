using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using chia.dotnet;
using System.Web;
using System.Text.Json;

namespace dig.server;

public class GatewayService
{
    private readonly DataLayerProxy _dataLayer;
    private readonly ChiaConfig _chiaConfig;
    private readonly StoreRegistryService _storeRegistryService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GatewayService> _logger;
    private readonly IConfiguration _configuration;
    private FileCacheService _fileCache;
    private readonly StoreUpdateNotifierService _storeUpdateNotifierService;

    public GatewayService(DataLayerProxy dataLayer,
                            ChiaConfig chiaConfig,
                            StoreRegistryService storeRegistryService,
                            IMemoryCache memoryCache,
                            ILogger<GatewayService> logger,
                            IConfiguration configuration)
    {
        _dataLayer = dataLayer;
        _chiaConfig = chiaConfig;
        _storeRegistryService = storeRegistryService;
        _memoryCache = memoryCache;
        _logger = logger;
        _configuration = configuration;
        _fileCache = new FileCacheService(@"C:\Temp\store-cache", _logger);
        _storeUpdateNotifierService = new StoreUpdateNotifierService(dataLayer, memoryCache, logger);

        _storeUpdateNotifierService.StartWatcher(storeId => InvalidateStore(storeId), TimeSpan.FromSeconds(15));
    }

    public WellKnown GetWellKnown(string baseUri) => new(xch_address: _configuration.GetValue("dig:XchAddress", "")!,
                                                                  known_stores_endpoint: $"{baseUri}/.well-known/known_stores",
                                                                  donation_address: _configuration.GetValue("dig:DonationAddress", "")!,
                                                                  server_version: GetAssemblyVersion());

    public bool HaveDataLayerConfig() => _chiaConfig.GetEndpoint("data_layer") is not null;

    private static string GetAssemblyVersion() => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

    public Task<bool> InvalidateStore(string storeId)
    {
        _fileCache.InvalidateStore(storeId, cacheKey =>
        {
            _logger.LogInformation("Removing {CacheKey} from memory cache", cacheKey);
            _memoryCache.Remove(cacheKey);
            return Task.CompletedTask;
        });
        return Task.FromResult(true);
    }

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
            var cacheKey = $"{storeId}-keys";
            // memory cache is used to cache the keys for 15 minutes
            var keys = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(15);
                _logger.LogInformation("Getting keys for {StoreId}", storeId.SanitizeForLog());

                var fsCacheValue = await _fileCache.GetValueAsync(cacheKey);
                if (fsCacheValue is not null)
                {
                    _logger.LogInformation("Got value for {StoreId} {Key} from file cache", storeId.SanitizeForLog(), storeId.SanitizeForLog());
                    return JsonSerializer.Deserialize<string[]>(fsCacheValue);
                }

                var datalayerValue = await _dataLayer.GetKeys(storeId, null, cancellationToken);
                await _fileCache.SetValueAsync(cacheKey, JsonSerializer.Serialize(datalayerValue ?? []));
                _logger.LogInformation("Got value for {StoreId} {Key} from DataLayer", storeId.SanitizeForLog(), storeId.SanitizeForLog());
                return datalayerValue;
            });

            return keys;
        }
        catch (Exception)
        {
            return null;  // 404 in the api
        }
        finally
        {
            await _storeUpdateNotifierService.RegisterStoreAsync(storeId);
        }
    }

    public async Task<string?> GetValue(string storeId, string key, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"{storeId}-{key}";
            var value = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(15);
                _logger.LogInformation("Getting value for {StoreId} {Key}", storeId.SanitizeForLog(), key.SanitizeForLog());

                var fsCacheValue = await _fileCache.GetValueAsync(cacheKey);
                if (fsCacheValue is not null)
                {
                    _logger.LogInformation("Got value for {StoreId} {Key} from file cache", storeId.SanitizeForLog(), key.SanitizeForLog());
                    return fsCacheValue;
                }

                var datalayerValue = await _dataLayer.GetValue(storeId, HttpUtility.UrlDecode(key), null, cancellationToken);
                await _fileCache.SetValueAsync(cacheKey, datalayerValue ?? "");
                _logger.LogInformation("Got value for {StoreId} {Key} from DataLayer", storeId.SanitizeForLog(), key.SanitizeForLog());
                return datalayerValue;
            });

            return value;
        }
        catch
        {
            return null; // 404 in the api
        }
        finally
        {
            await _storeUpdateNotifierService.RegisterStoreAsync(storeId);
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
