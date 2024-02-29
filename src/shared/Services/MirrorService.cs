using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace dig;

public sealed class MirrorService(IHttpClientFactory httpClientFactory,
                                    ILogger<MirrorService> logger)
{
    private readonly ILogger<MirrorService> _logger = logger;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly JsonSerializerSettings _settings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    };

    public async IAsyncEnumerable<Store> FetchLatest(string uri, [EnumeratorCancellation] CancellationToken stoppingToken)
    {
        _logger.LogInformation("Fetching latest mirrors from {uri}", uri);
        var currentPage = 1;
        var totalPages = 0; // we won't know actual total pages until we get the first page

        do
        {
            var page = await GetPage(_httpClient, uri, currentPage, stoppingToken);
            totalPages = page.TotalPages;

            foreach (var singleton in page.Mirrors)
            {
                yield return singleton;
            }

            currentPage++;
        } while (currentPage <= totalPages && !stoppingToken.IsCancellationRequested);
    }

    private async Task<PageRecord> GetPage(HttpClient httpClient, string uri, int currentPage, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Fetching page {currentPage} from {uri}", currentPage, uri);
            using var response = await httpClient.GetAsync($"{uri}?page={currentPage}", stoppingToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(stoppingToken);
            return JsonConvert.DeserializeObject<PageRecord>(responseBody, _settings) ?? throw new InvalidOperationException("Failed to fetch mirrors");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("There was a problem fetching the singleton list: {Message}", ex.InnerException?.Message ?? ex.Message);
            // this is not fatal to the process, so return an empty page
            return new PageRecord();
        }
    }
}
