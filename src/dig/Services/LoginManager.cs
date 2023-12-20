using Microsoft.AspNetCore.DataProtection;
using System.Dynamic;
using System.Text;
using System.Net.Http.Json;
namespace dig;

internal class LoginManager(IDataProtectionProvider provider,
                        AppStorage appStorage,
                        DnsService dnsService,
                        IHttpClientFactory httpClientFactory,
                        ILogger<LoginManager> logger,
                        IConfiguration configuration)
{
    private readonly IDataProtector _protector = provider.CreateProtector("DataLayer-Storage.datalayer.place.v3");
    private readonly AppStorage _appStorage = appStorage;
    private readonly DnsService _dnsService = dnsService;
    private readonly ILogger<LoginManager> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("datalayer.storage");

    public async Task<string?> Login(string accessToken, string secretKey, CancellationToken stoppingToken = default)
    {
        // Encode the username and password with base64
        string encodedAuth = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(accessToken + ":" + secretKey));

        dynamic myPlace = await GetMyPlace(encodedAuth, stoppingToken);
        var token = myPlace?.proxy_key as object;
        var proxyKey = token?.ToString();
        if (string.IsNullOrEmpty(proxyKey))
        {
            _logger.LogError("Login failed.");
            return null;
        }

        // if we have gotten here we're good to go so securely store the credentials
        var protectedAuth = _protector.Protect(encodedAuth);
        _appStorage.Save("auth", protectedAuth);

        return proxyKey;
    }

    public void LogOut()
    {
        _appStorage.Remove("auth");
    }

    public async Task<dynamic> GetMyPlace(string encodedAuth, CancellationToken stoppingToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedAuth);

        var response = await _httpClient.GetAsync("user/v1/me", stoppingToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExpandoObject>(stoppingToken) ?? throw new Exception("Login failed.");
    }

    public string? GetCredentials()
    {
        var protectedAuth = _appStorage.Load("auth");
        if (string.IsNullOrEmpty(protectedAuth))
        {
            return null;
        }

        return _protector.Unprotect(protectedAuth);
    }
}
