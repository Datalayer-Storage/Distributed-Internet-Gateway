using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using chia.dotnet;

namespace dig.server;

public class MeshNetworkRoutingService(ChiaConfig chiaConfig,
                                        DataLayerProxy dataLayer,
                                        ServerCoinService serverCoinService,
                                        ILogger<MeshNetworkRoutingService> logger,
                                        IConfiguration configuration,
                                        IMemoryCache cache)
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ServerCoinService _serverCoinService = serverCoinService;
    private readonly ILogger<MeshNetworkRoutingService> _logger = logger;
    private readonly HttpClient _httpClient = new();
    private readonly IConfiguration _configuration = configuration;
    private readonly IMemoryCache _cache = cache;

    private string[]? GetRedirectUrls(string storeId)
    {
        string cacheKey = $"RedirectUrls-{storeId}";
        // Check if the URL list is already cached
        if (!_cache.TryGetValue(cacheKey, out string[]? cachedUrls))
        {
            var coins = _serverCoinService.GetCoins(storeId);
            if (coins.Any())
            {
                var allUrls = coins.Select(coin => coin.urls).ToArray();
                var flattenedUrls = allUrls.SelectMany(urls => ((List<object>)urls).Select(url => (string)url)).ToArray();

                _logger.LogInformation("Found {count} URLs for store {storeId}", flattenedUrls.Length, storeId.SanitizeForLog());

                // Cache the flattened URLs with a 5-minute expiration
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKey, flattenedUrls, cacheEntryOptions);

                return flattenedUrls;
            }
            else
            {
                // Cache an empty array to avoid repeatedly fetching for non-existent URLs
                _cache.Set(cacheKey, Array.Empty<string>(), new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(5)));
                return [];
            }
        }

        return cachedUrls;
    }

    public async Task<string?> GetMeshNetworkContentsAsync(string storeId, string? key) => await FetchMeshNetworkData(storeId, key, false);

    public async Task<string?> GetMeshNetworkLocationAsync(string storeId, string? key) => await FetchMeshNetworkData(storeId, key, true);

    // The first URL is usually the source of the datastore so lets attempt to get the data from there first
    // This is because will always have the most up to date data while the rest of the network is still propagating.
    // Then move on to the rest of the network if the first is slow or down.
    private async Task<string?> FetchMeshNetworkData(string storeId, string? key, bool returnRedirectUrl)
    {
        var urls = GetRedirectUrls(storeId);

        if (urls is null || urls.Length == 0)
        {
            _logger.LogWarning("No URLs found for store {storeId}", storeId.SanitizeForLog());
            return null;
        }

        // Apply cache filter to the first URL
        var firstUrl = urls.FirstOrDefault(url => !_cache.TryGetValue(url, out _));

        // Filter and shuffle the remaining URLs, excluding the first URL if it's already selected
        var remainingUrls = urls.SkipWhile(url => url == firstUrl).Where(url => !_cache.TryGetValue(url, out _)).OrderBy(_ => Guid.NewGuid());

        // Combine the first URL (if not cached) with the filtered remaining URLs
        var orderedUrls = firstUrl != null ? new[] { firstUrl }.Concat(remainingUrls) : remainingUrls;

        foreach (var url in orderedUrls)
        {
            var apiUrl = $"{url}/api/status/{storeId}";
            var redirectUrl = key != null ? $"{url}/{storeId}/{key}" : $"{url}/{storeId}";

            try
            {
                var response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<DataLayerSyncStatus>(data);
                    var rootHistory = await _dataLayer.GetRootHistory(storeId, default);
                    var lastRootHistoryItem = rootHistory?.LastOrDefault();

                    if (json == null || lastRootHistoryItem == null || lastRootHistoryItem.RootHash != json.RootHash)
                    {
                        continue;
                    }

                    if (returnRedirectUrl)
                    {
                        _logger.LogInformation("Found 200 OK for URL {url}", redirectUrl.SanitizeForLog());
                        return redirectUrl;
                    }

                    var redirectResponse = await _httpClient.GetAsync(redirectUrl);
                    if (redirectResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Fetching content for URL {url}", redirectUrl.SanitizeForLog());
                        return await redirectResponse.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking URL {url}", apiUrl.SanitizeForLog());
                _cache.Set(url, false, TimeSpan.FromHours(1));
            }
        }

        return null;
    }
}
