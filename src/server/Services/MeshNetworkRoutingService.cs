using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Threading.Tasks;

namespace dig.server;

public class MeshNetworkRoutingService(ChiaConfig chiaConfig,
                                        ServerCoinService serverCoinService,
                                        ILogger<MeshNetworkRoutingService> logger,
                                        IConfiguration configuration)
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly ServerCoinService _serverCoinService = serverCoinService;
    private readonly ILogger<MeshNetworkRoutingService> _logger = logger;
    private readonly HttpClient _httpClient = new();
    private readonly IConfiguration _configuration = configuration;

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

    public async Task<string?> GetMeshNetworkLocationAsync(string storeId, string? key)
    {
        var urls = GetRedirectUrls(storeId);

        // Shuffle the URLs using the Guid technique for randomness
        var shuffledUrls = urls.OrderBy(url => Guid.NewGuid()).ToList();

        foreach (var url in shuffledUrls)
        {
            var requestUrl = $"{url}/{storeId}"; // Construct the request URL


            if (key is not null)
            {
                requestUrl += $"/{key}";
            }

            _logger.LogInformation("Checking HEAD FOR URL {url}", requestUrl);

            // Create a HEAD request
            var request = new HttpRequestMessage(HttpMethod.Head, requestUrl);

            try
            {
                // Send the HEAD request
                var response = await _httpClient.SendAsync(request);

                _logger.LogInformation("Found {statusCode} for URL {url}", response.StatusCode, requestUrl);

                // Check if the status code is 200 OK
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Found 200 OK for URL {url}", requestUrl);
                    return requestUrl; // Return the URL if 200 OK
                }
            }
            catch (Exception)
            {
                // Ignore exceptions and try the next URL
            }
        }

        return null; // Return null if no URL returns 200 OK
    }
}
