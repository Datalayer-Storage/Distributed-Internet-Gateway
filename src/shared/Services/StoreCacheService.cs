using chia.dotnet;

namespace dig;

public class StoreCacheService(DataLayerProxy dataLayer,
                                FileCacheService fileCache,
                                ILogger<CacheService> logger)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly FileCacheService _fileCache = fileCache;
    private readonly ILogger<CacheService> _logger = logger;

    public async Task VerifyStoreCache(string storeId, CancellationToken cancellationToken)
    {
        if (!await StoreCacheIsValid(storeId, cancellationToken))
        {
            _logger.LogWarning("Invalidating cache for {StoreId}", storeId.SanitizeForLog());
            InvalidateStoreCache(storeId);
        }
    }

    public async Task<bool> StoreCacheIsValid(string storeId, CancellationToken cancellationToken)
    {
        var lastRoot = await _fileCache.GetValueAsync<RootHash>($"{storeId}-last-root", cancellationToken);
        var currentRoot = await _dataLayer.GetRoot(storeId, cancellationToken);

        if (lastRoot is null)
        {
            await _fileCache.SetValueAsync($"{storeId}-last-root", currentRoot, cancellationToken);

            return false;
        }

        return currentRoot.Hash == lastRoot.Hash;
    }

    public void InvalidateStoreCache(string storeId) => _fileCache.RemoveStore(storeId);

    public async Task CacheStore(string storeId, CancellationToken cancellationToken)
    {
        InvalidateStoreCache(storeId);

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
