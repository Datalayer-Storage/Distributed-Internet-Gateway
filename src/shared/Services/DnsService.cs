using System.Dynamic;
using System.Net.Http.Json;

namespace dig;

public sealed class DnsService(IHttpClientFactory httpClientFactory,
                                    ILogger<DnsService> logger,
                                    IConfiguration configuration)
{
    private readonly ILogger<DnsService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("datalayer.storage");

    public async Task<string> ResolveHostUrl(int port, string? url, CancellationToken stoppingToken)
    {
        if (!string.IsNullOrEmpty(url))
        {
            return url.ToString();
        }

        return await GetHostUri(port, stoppingToken) ?? throw new Exception("Failed to get public ip.");
    }

    public async Task<string?> GetMirrorUri(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue("dig:DataLayerMirrorPort", 8575);

        return await GetHostUri(port, stoppingToken);
    }

    public async Task<string?> GetDigServerUri(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue("dig:DigServerPort", 41410);

        return await GetHostUri(port, stoppingToken);
    }

    private async Task<string?> GetHostUri(int port, CancellationToken stoppingToken)
    {
        // config file takes precedence
        var host = _configuration.GetValue("dig:HostName", "");
        if (string.IsNullOrEmpty(host))
        {
            host = await GetPublicIPAdress(stoppingToken);
        }

        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var scheme = _configuration.GetValue("dig:HostScheme", "http");
        return $"{scheme}://{host}:{port}";
    }

    public async Task<string?> GetPublicIPAdress(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Getting public ip address");

            var response = await _httpClient.GetAsync("/user/v1/get_user_ip", stoppingToken);
            dynamic ip = await response.Content.ReadFromJsonAsync<ExpandoObject>(stoppingToken) ?? throw new Exception("Get public ip address failed.");

            return ip?.ip_address?.ToString()?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get public ip address: {Message}", ex.InnerException?.Message ?? ex.Message);
            return null;
        }
    }
}
