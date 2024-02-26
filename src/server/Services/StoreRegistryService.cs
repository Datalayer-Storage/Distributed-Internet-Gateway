using System.Collections.Concurrent;

namespace dig.server;

public sealed class StoreRegistryService(MirrorService mirrorService,
                                            ILogger<StoreRegistryService> logger, 
                                            IConfiguration configuration)
{
    private readonly IDictionary<string, Store> _storeNames = new ConcurrentDictionary<string, Store>();
    private readonly MirrorService _mirrorService = mirrorService;
    private readonly ILogger<StoreRegistryService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public Store GetStore(string key)
    {
        return _storeNames.TryGetValue(key, out var store) ? store : new Store { singleton_id = key };
    }

    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        try
        {
            var mirrorListUri = _configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage/") + "mirrors/v1/list_all";

            await foreach (var store in _mirrorService.FetchLatest(mirrorListUri, cancellationToken))
            {
                // if there is no verified_name use the singleton_id as the name
                _storeNames[store.singleton_id] = store;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
        }
    }
}
