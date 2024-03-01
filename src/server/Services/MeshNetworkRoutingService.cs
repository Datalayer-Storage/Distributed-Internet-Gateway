using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using chia.dotnet;

namespace dig.server;

public class MeshNetworkRoutingService(ChiaConfig chiaConfig,
                                        DataLayerProxy dataLayer,
                                        ServerCoinService serverCoinService,
                                        ILogger<MeshNetworkRoutingService> logger,
                                        IConfiguration configuration, IMemoryCache cache)
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ServerCoinService _serverCoinService = serverCoinService;
    private readonly ILogger<MeshNetworkRoutingService> _logger = logger;
    private readonly HttpClient _httpClient = new();
    private readonly IConfiguration _configuration = configuration;
    private readonly IMemoryCache _cache = cache;

    private string[] GetRedirectUrls(string storeId)
    {
        var coins = _serverCoinService.GetCoins(storeId);
        if (coins.Any())
        {
            // Assuming coin.urls is an IEnumerable of some type that can be converted to string
            var allUrls = coins.Select(coin => coin.urls).ToArray();

            // Convert each List<object> in 'allUrls' to string[], then flatten
            var flattenedUrls = allUrls.SelectMany(urls => ((List<object>)urls).Select(url => (string)url)).ToArray();


            // _fileCacheService.SetValueAsync(cacheKey, allUrls, default);
            _logger.LogInformation("Found {count} URLs for store {storeId}", flattenedUrls.Length, storeId.SanitizeForLog());
            return flattenedUrls;
        }

        return [];
    }

    public async Task<string?> GetMeshNetworkContentsAsync(string storeId, string? key)
    {
        var urls = GetRedirectUrls(storeId);

        // Filter out URLs that are in the cache (not online in the past 24 hours)
        var filteredUrls = urls.Where(url => !_cache.TryGetValue(url, out _)).ToList();

        // Shuffle the filtered URLs
        var shuffledUrls = filteredUrls.OrderBy(url => Guid.NewGuid()).ToList();

        foreach (var url in shuffledUrls)
        {
            var apiUrl = $"{url}/api/status/{storeId}";
            var redirectUrl = key != null ? $"{url}/{storeId}/{key}" : $"{url}/{storeId}";

            _logger.LogInformation("Checking URL {url}", apiUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            try
            {
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<chia.dotnet.DataLayerSyncStatus>(data);

                    if (json != null)
                    {
                        var rootHistory = await _dataLayer.GetRootHistory(storeId, default);
                        var lastRootHistoryItem = rootHistory?.LastOrDefault();

                        if (lastRootHistoryItem != null && lastRootHistoryItem.RootHash != json.RootHash)
                        {
                            continue; // Skip this URL and continue with the next one
                        }

                        // If checks pass, make a GET request to the redirectUrl and return its content
                        _logger.LogInformation("Fetching content for URL {url}", redirectUrl);
                        var redirectRequest = new HttpRequestMessage(HttpMethod.Get, redirectUrl);
                        var redirectResponse = await _httpClient.SendAsync(redirectRequest);
                        if (redirectResponse.IsSuccessStatusCode)
                        {
                            var redirectData = await redirectResponse.Content.ReadAsStringAsync();
                            return redirectData; // Return the content of the redirect URL
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking URL {url}", apiUrl);

                // If the server is offline, blacklist it for 1 hour before trying again
                _cache.Set(url, false, TimeSpan.FromHours(1));
                // Ignore exceptions and try the next URL
            }
        }

        return null; // Return null if no URL passes the checks
    }


    public async Task<string?> GetMeshNetworkLocationAsync(string storeId, string? key)
    {
        var urls = GetRedirectUrls(storeId);

        // Filter out URLs that are in the cache (not online in the past 24 hours)
        var filteredUrls = urls.Where(url => !_cache.TryGetValue(url, out _)).ToList();

        // Shuffle the filtered URLs
        var shuffledUrls = filteredUrls.OrderBy(url => Guid.NewGuid()).ToList();

        foreach (var url in shuffledUrls)
        {
            var apiUrl = $"{url}/api/status/{storeId}";
            var redirectUrl = key != null ? $"{url}/{storeId}/{key}" : $"{url}/{storeId}";

            _logger.LogInformation("Checking URL {url}", apiUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            try
            {
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<chia.dotnet.DataLayerSyncStatus>(data);

                    if (json != null)
                    {
                        var rootHistory = await _dataLayer.GetRootHistory(storeId, default);
                        var lastRootHistoryItem = rootHistory?.LastOrDefault();

                        if (lastRootHistoryItem != null && lastRootHistoryItem.RootHash != json.RootHash)
                        {
                            continue; // Skip this URL and continue with the next one
                        }

                        _logger.LogInformation("Found 200 OK for URL {url}", redirectUrl);
                        return redirectUrl; // Return the redirect URL if checks pass
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking URL {url}", apiUrl);

                // If the server is offline, blacklist it for 1 hour before trying again
                _cache.Set(url, false, TimeSpan.FromHours(1));
                // Ignore exceptions and try the next URL
            }
        }

        return null; // Return null if no URL passes the checks
    }
}
