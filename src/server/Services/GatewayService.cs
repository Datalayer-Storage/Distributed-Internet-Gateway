using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using chia.dotnet;
using System.Web;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace dig.server
{
    public sealed class GatewayService
    {
        private readonly DataLayerProxy _dataLayer;
        private readonly ChiaConfig _chiaConfig;
        private readonly StoreRegistryService _storeRegistryService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<GatewayService> _logger;
        private readonly IConfiguration _configuration;
        private readonly FileCacheService _fileCache;
        private readonly StoreUpdateNotifierService _storeUpdateNotifierService;
        private readonly ConcurrentDictionary<string, HashSet<string>> _storeCacheKeys = new();

        public GatewayService(DataLayerProxy dataLayer, ChiaConfig chiaConfig, StoreRegistryService storeRegistryService, IMemoryCache memoryCache, ILogger<GatewayService> logger, IConfiguration configuration)
        {
            _dataLayer = dataLayer;
            _chiaConfig = chiaConfig;
            _storeRegistryService = storeRegistryService;
            _memoryCache = memoryCache;
            _logger = logger;
            _configuration = configuration;

            var appStorage = new AppStorage(".dig");
            _fileCache = new FileCacheService(appStorage.UserSettingsFilePath);

            _storeUpdateNotifierService = new StoreUpdateNotifierService(this, memoryCache, logger);
            _storeUpdateNotifierService.StartWatcher(storeId => InvalidateCache(storeId), TimeSpan.FromMinutes(2));
        }

        public WellKnown GetWellKnown(string baseUri) => new WellKnown(
            xch_address: _configuration.GetValue("dig:XchAddress", "")!,
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
            }) ?? Array.Empty<string>();
        }

        public async Task<IEnumerable<Store>> GetKnownStoresWithNames()
        {
            var stores = await GetKnownStores();
            return stores.Select(_storeRegistryService.GetStore);
        }

        private IEnumerable<string> GetCacheKeysForStoreId(string storeId)
        {
            if (_storeCacheKeys.TryGetValue(storeId, out var cacheKeys))
            {
                return cacheKeys;
            }

            return Enumerable.Empty<string>();
        }

        private async Task AddCacheKeyForStoreIdAsync(string storeId, string cacheKey, object value)
        {
            // Add to MemoryCache with a fixed duration of 2 minutes
            _memoryCache.Set(cacheKey, value, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(15)
            });

            // Serialize the value as JSON for FileCache
            var jsonValue = JsonSerializer.Serialize(value);

            // Add to FileCache
            await _fileCache.SetValueAsync(cacheKey, jsonValue);

            // Update the _storeCacheKeys dictionary to track this cache key
            _storeCacheKeys.AddOrUpdate(storeId, new HashSet<string> { cacheKey },
                (key, existingHashSet) =>
                {
                    existingHashSet.Add(cacheKey);
                    return existingHashSet;
                });
        }


        public async Task<IEnumerable<string>?> GetKeys(string storeId, CancellationToken cancellationToken)
        {
            var cacheKey = $"{storeId}-keys";
            if (!_memoryCache.TryGetValue(cacheKey, out IEnumerable<string>? keys))
            {
                var jsonKeys = await _fileCache.GetValueAsync(cacheKey);
                if (jsonKeys == null)
                {
                    keys = await _dataLayer.GetKeys(storeId, null, cancellationToken);
                    if (keys != null)
                    {
                        await AddCacheKeyForStoreIdAsync(storeId, cacheKey, keys);
                    }
                }
                else
                {
                    try
                    {
                        keys = JsonSerializer.Deserialize<IEnumerable<string>>(jsonKeys);
                        _memoryCache.Set(cacheKey, keys, TimeSpan.FromMinutes(15));
                    }
                    catch (JsonException e)
                    {
                        _logger.LogError(e, "Error deserializing keys from file cache for storeId {StoreId}", storeId);
                        // Consider handling the error appropriately, maybe invalidate the corrupted cache entry
                    }
                }
            }

            return keys;
        }

        public async Task<string?> GetValue(string storeId, string key, CancellationToken cancellationToken)
        {
            var cacheKey = $"{storeId}-{key}";
            if (!_memoryCache.TryGetValue(cacheKey, out string? value))
            {
                value = await _fileCache.GetValueAsync(cacheKey);
                if (value == null)
                {
                    value = await _dataLayer.GetValue(storeId, HttpUtility.UrlDecode(key), null, cancellationToken);
                    if (value != null)
                    {
                        await _fileCache.SetValueAsync(cacheKey, value);
                        await AddCacheKeyForStoreIdAsync(storeId, cacheKey, value);
                    }
                }
                else
                {
                    _memoryCache.Set(cacheKey, value, TimeSpan.FromMinutes(15));
                }
            }

            return value;
        }

        public async Task<bool> InvalidateCache(string storeId)
        {
            var cacheKeys = GetCacheKeysForStoreId(storeId);

            foreach (var key in cacheKeys)
            {
                _memoryCache.Remove(key);
                _fileCache.Invalidate(key);
            }

            ClearCacheKeysForStoreId(storeId);

            return true;
        }

        private void ClearCacheKeysForStoreId(string storeId)
        {
            _storeCacheKeys.TryRemove(storeId, out _);
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
}
