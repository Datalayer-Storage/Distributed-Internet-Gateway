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

    /**
    * Fetches data from a mesh network of URLs associated with a store ID and an optional key.
    * The first URL in the list is prioritized since the first url is usually the store owner, 
    * followed by a randomized order of HTTPS URLs, then domain-named URLs, and finally any 
    * remaining URLs, also randomized. This hurestic sorts the url list in a best guess order of reliability.
    * 
    * @param storeId The store ID for which to fetch the data.
    * @param key An optional key for the specific data to fetch.
    * @param returnRedirectUrl A flag indicating whether to return the redirect URL instead of the content.
    * @return A Task that represents the asynchronous operation, which wraps the fetched data or redirect URL.
    */
    private async Task<string?> FetchMeshNetworkData(string storeId, string? key, bool returnRedirectUrl)
    {
        var urls = GetRedirectUrls(storeId);

        if (urls is null || urls.Length == 0)
        {
            _logger.LogWarning("No URLs found for store {storeId}", storeId.SanitizeForLog());
            return null;
        }

        var firstUrl = urls.FirstOrDefault();

        // Filter and shuffle the remaining URLs based on their type, excluding the first URL
        var httpsUrls = urls.Where(url => url.StartsWith("https") && url != firstUrl).OrderBy(_ => Guid.NewGuid());
        var domainNamedUrls = urls.Where(url => !url.StartsWith("https") && Uri.CheckHostName(url) != UriHostNameType.IPv6 && Uri.CheckHostName(url) != UriHostNameType.IPv4 && url != firstUrl).OrderBy(_ => Guid.NewGuid());
        var remainingUrls = urls.Except(httpsUrls).Except(domainNamedUrls).Except(new[] { firstUrl }).OrderBy(_ => Guid.NewGuid());

        // Combine all sorted and filtered URLs
        var orderedUrls = (new[] { firstUrl }).Concat(httpsUrls).Concat(domainNamedUrls).Concat(remainingUrls).Where(url => url != null && !_cache.TryGetValue(url, out _));

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
