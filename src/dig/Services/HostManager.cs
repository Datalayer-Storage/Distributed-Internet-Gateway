using System.Net.Http.Json;
namespace dig;


internal class HostManager(DnsService denService,
                        IHttpClientFactory httpClientFactory,
                        ILogger<HostManager> logger)
{
    private readonly DnsService _dnsService = denService;
    private readonly ILogger<HostManager> _logger = logger;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("datalayer.storage");

    public async Task CheckHost(string host, CancellationToken token = default)
    {
        var hostToCheck = await GetHost(host, token);
        if (string.IsNullOrEmpty(hostToCheck))
        {
            _logger.LogWarning("No host specified and no public ip address found.");
        }
        else
        {
            _logger.LogInformation("Checking {host}", hostToCheck);

            var data = new { hostname = hostToCheck };
            var response = await _httpClient.PostAsJsonAsync("mirrors/v1/check_connection", data, token);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(token);

            Console.WriteLine(content);
        }
    }

    private async Task<string?> GetHost(string? host, CancellationToken token = default)
    {
        if (!string.IsNullOrEmpty(host))
        {
            return host;
        }

        return await _dnsService.GetHostUri(8575, token);
    }
}
