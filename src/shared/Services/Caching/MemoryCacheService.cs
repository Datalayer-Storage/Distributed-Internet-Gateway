using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace dig.caching;

/// <summary>
/// The cache service that does not caching. Used to disable caching altogether.
/// </summary>
public class MemoryCacheService(IMemoryCache memoryCache,
                                ILogger<MemoryCacheService> logger,
                                IConfiguration configuration) : IObjectCache
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<MemoryCacheService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private CancellationTokenSource _cacheResetToken = new();
    private readonly object _lock = new();

    public async Task<TItem?> GetOrCreateAsync<TItem>(string storeId, string rootHash, string key, Func<Task<TItem>> factory, CancellationToken token)
    {
        var cacheKey = string.Concat(storeId, rootHash, key).MD5Hash();
        return await _memoryCache.GetOrCreateAsync(cacheKey, async (entry) =>
        {
            _logger.LogWarning("Memory cache miss for {key}", key.SanitizeForLog());

            // this allows us to reset the cache when we want to
            entry.AddExpirationToken(new CancellationChangeToken(_cacheResetToken.Token));
            entry.SlidingExpiration = TimeSpan.FromMinutes(_configuration.GetValue("dig:MemoryCacheSlidingExpirationMinutes", 15));
            return await factory();
        });
    }

    public async Task<TItem?> GetValueAsync<TItem>(string storeId, string rootHash, string key, CancellationToken token)
    {
        var cacheKey = string.Concat(storeId, rootHash, key).MD5Hash();

        if (_memoryCache.TryGetValue(cacheKey, out TItem? value))
        {
            return value;
        }

        await Task.CompletedTask;

        return default;
    }

    public async Task SetValueAsync<TItem>(string storeId, string rootHash, string key, TItem? value, CancellationToken token)
    {
        var cacheKey = string.Concat(storeId, rootHash, key).MD5Hash();

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .AddExpirationToken(new CancellationChangeToken(_cacheResetToken.Token));

        cacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(_configuration.GetValue("dig:MemoryCacheSlidingExpirationMinutes", 15));

        _memoryCache.Set(cacheKey, value, cacheEntryOptions);

        await Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cacheResetToken.Cancel();
            _cacheResetToken.Dispose();
            _cacheResetToken = new CancellationTokenSource();
        }
    }

    public void RemoveStore(string storeId)
    {
        // since the cache key includes the store and the root hash this shouldn't be necessary
        // for the memory cache, which doesn't support bulk removal
        //
        // orphaned keys will be removed by the sliding expiration
        // and the cache reset token
    }
}
