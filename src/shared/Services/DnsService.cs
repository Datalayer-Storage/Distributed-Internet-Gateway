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

    public async Task<string?> GetHostUri(CancellationToken stoppingToken)
    {
        // config file takes precedence
        var host = _configuration["dig:MirrorHostUri"];
        if (!string.IsNullOrEmpty(host))
        {
            return host;
        }

        var publicIpAddress = await GetPublicIPAdress(stoppingToken);
        publicIpAddress = publicIpAddress?.Trim();
        if (!string.IsNullOrEmpty(publicIpAddress))
        {
            return $"http://{publicIpAddress}:8575";
        }

        return null;
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
