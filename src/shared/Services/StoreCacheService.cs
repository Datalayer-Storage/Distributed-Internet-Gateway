using chia.dotnet;

namespace dig;

public class StoreCacheService(DataLayerProxy dataLayer,
                                FileCacheService fileCache,
                                ILogger<StoreCacheService> logger)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly FileCacheService _fileCache = fileCache;
    private readonly ILogger<StoreCacheService> _logger = logger;

    public async Task<string> RefreshStoreRootHash(string storeId, CancellationToken cancellationToken)
    {
        var currentRoot = await _dataLayer.GetRoot(storeId, cancellationToken);
        var cachedRootHash = await _fileCache.GetValueAsync<RootHash>($"{storeId}-last-root", cancellationToken);

        // the current hash doesn't match the persistent cache
        if (cachedRootHash?.Hash != currentRoot.Hash)
        {
            _logger.LogWarning("Invalidating cache for {StoreId}", storeId.SanitizeForLog());
            _fileCache.RemoveStore(storeId);
            await _fileCache.SetValueAsync($"{storeId}-last-root", currentRoot, cancellationToken);
        }

        return currentRoot.Hash;
    }

    public async Task CacheStore(string storeId, CancellationToken cancellationToken)
    {
        _fileCache.RemoveStore(storeId);

        var lastRoot = await _dataLayer.GetRoot(storeId, cancellationToken);
        await _fileCache.SetValueAsync($"{storeId}-last-root", lastRoot, cancellationToken);

        var storeKeys = await _dataLayer.GetKeys(storeId, lastRoot.Hash, cancellationToken);
        if (storeKeys != null)
        {
            await _fileCache.SetValueAsync($"{storeId}-keys", storeKeys, cancellationToken);

            foreach (var key in storeKeys)
            {
                _logger.LogInformation("Pre-caching key {key} for storeId {storeId}.", key.SanitizeForLog(), storeId.SanitizeForLog());
                var value = await _dataLayer.GetValue(storeId, key, lastRoot.Hash, cancellationToken);
                if (value != null)
                {
                    await _fileCache.SetValueAsync($"{storeId}-{key}", value, cancellationToken);
                }
            }
        }
    }
}
