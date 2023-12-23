using System.Collections.Concurrent;

namespace dig.server;

public sealed class StoreRegistryService(MirrorService mirrorService,
                                            ILogger<StoreRegistryService> logger,
                                            IConfiguration configuration
                                            )
{
    private IDictionary<string, string> _storeNames = new ConcurrentDictionary<string, string>();
    private readonly MirrorService _mirrorService = mirrorService;
    private readonly ILogger<StoreRegistryService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public string GetStoreName(string key)
    {
        return _storeNames.TryGetValue(key, out var name) ? name : key;
    }

    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        try
        {
            var mirrorListUri = _configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage/") + "mirrors/v1/list_all";

            await foreach (var store in _mirrorService.FetchLatest(mirrorListUri, cancellationToken))
            {
                _storeNames[store.singleton_id] = string.IsNullOrEmpty(store.verified_name) ? store.singleton_id : store.verified_name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
        }
    }

}
