internal sealed class DnsService(ILogger<DnsService> logger, IConfiguration configuration)
{
    private readonly ILogger<DnsService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task<string?> GetHostUri(CancellationToken stoppingToken)
    {
        // config file takes precedence
        var host = _configuration["App:MirrorHostUri"];
        if (!string.IsNullOrEmpty(host))
        {
            return host;
        }

        var publicIpAddress = await GetPublicIPAdress(stoppingToken);
        if (!string.IsNullOrEmpty(publicIpAddress))
        {
            return $"http://{publicIpAddress}:8575";
        }

        return null;
    }

    public async Task<string> GetPublicIPAdress(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Getting public ip address");
            using var httpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            return await httpClient.GetStringAsync("https://api.ipify.org", stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get public ip address: {Message}", ex.InnerException?.Message ?? ex.Message);
            return string.Empty;
        }
    }
}
