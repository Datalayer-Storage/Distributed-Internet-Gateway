using System.Net.Http.Json;

namespace dig;

internal sealed class DynDnsService(IHttpClientFactory httpClientFactory,
                                    DnsService dnsService,
                                    ILogger<DynDnsService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("datalayer.storage");
    private readonly DnsService _dnsService = dnsService;
    private readonly ILogger<DynDnsService> _logger = logger;

    public async Task<string?> UpdateIP(string encodedAuth, CancellationToken stoppingToken = default)
    {
        try
        {
            var ip = await _dnsService.GetPublicIPAdress(stoppingToken);
            if (string.IsNullOrEmpty(ip))
            {
                _logger.LogError("Could not retrieve public IP address.");
                return null;
            }

            _logger.LogInformation("{ip}", ip);

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedAuth);

            var data = new { ip_address = ip };
            var response = await _httpClient.PutAsJsonAsync("user/v1/update_user_ip", data, stoppingToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError("{Message}", ex.InnerException?.Message ?? ex.Message);
            return null;
        }
    }
}
