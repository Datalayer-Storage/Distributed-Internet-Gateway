using System.Net.Http.Json;

internal class HostManager(DnsService denService,
                        ILogger<HostManager> logger,
                        IConfiguration configuration)
{
    private readonly DnsService _dnsService = denService;
    private readonly ILogger<HostManager> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task CheckHost(string host, CancellationToken token = default)
    {
        var hostToCheck = await GetHost(host, token);
        if (string.IsNullOrEmpty(hostToCheck))
        {
            _logger.LogWarning("No host specified and no public ip address found.");
        }
        else
        {
            using var httpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            _logger.LogInformation("Checking {host}", hostToCheck);

            var data = new { hostname = hostToCheck };
            var checkConnectionUri = _configuration.GetValue("App:MirrorServiceUri", "https://api.datalayer.storage/mirrors/v1/") + "check_connection";
            var response = await httpClient.PostAsJsonAsync(checkConnectionUri, data, token);
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

        return await _dnsService.GetHostUri(token);
    }
}
