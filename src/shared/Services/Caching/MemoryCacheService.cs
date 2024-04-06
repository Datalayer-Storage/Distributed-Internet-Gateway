using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace dig.caching;

/// <summary>
/// This cache service uses the built-in .NET Core memory cache to store objects in memory.
/// </summary>
public class MemoryCacheService(IMemoryCache memoryCache,
                                ILogger<MemoryCacheService> logger,
                                IConfiguration configuration) : IObjectCache, IDisposable
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<MemoryCacheService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private CancellationTokenSource _cacheResetTokenSource = new();
    private bool disposedValue;
    private readonly object _lock = new();

    public async Task<TItem?> GetOrCreateAsync<TItem>(string topic, string objectId, string rootHash, string key, Func<Task<TItem>> factory, CancellationToken token)
    {
        var cacheKey = string.Concat(topic, objectId, rootHash, key).MD5Hash();
        return await _memoryCache.GetOrCreateAsync(cacheKey, async (entry) =>
        {
            _logger.LogWarning("Memory cache miss for {key}", key.SanitizeForLog());

            // this allows us to reset the cache when we want to
            entry.AddExpirationToken(new CancellationChangeToken(_cacheResetTokenSource.Token));
            entry.SlidingExpiration = TimeSpan.FromMinutes(_configuration.GetValue("dig:MemoryCacheSlidingExpirationMinutes", 15));
            return await factory();
        });
    }

    public async Task<TItem?> GetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, CancellationToken token)
    {
        var cacheKey = string.Concat(topic, objectId, rootHash, key).MD5Hash();

        if (_memoryCache.TryGetValue(cacheKey, out TItem? value))
        {
            return value;
        }

        await Task.CompletedTask;

        return default;
    }

    public async Task SetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, TItem? value, CancellationToken token)
    {
        var cacheKey = string.Concat(topic, objectId, rootHash, key).MD5Hash();

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .AddExpirationToken(new CancellationChangeToken(_cacheResetTokenSource.Token));

        cacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(_configuration.GetValue("dig:MemoryCacheSlidingExpirationMinutes", 15));

        _memoryCache.Set(cacheKey, value, cacheEntryOptions);

        await Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cacheResetTokenSource.Cancel();
            _cacheResetTokenSource.Dispose();
            _cacheResetTokenSource = new CancellationTokenSource();
        }
    }

    public void Clear(string topic)
    {
        // TODO
        // this clears all topics - need token source per topic to clear just one
        lock (_lock)
        {
            _cacheResetTokenSource.Cancel();
            _cacheResetTokenSource.Dispose();
            _cacheResetTokenSource = new CancellationTokenSource();
        }
    }

    public void RemoveValue(string topic, string objectKey)
    {
        // since the cache key includes the store and the root hash this
        // is ok to be a no-op for the memory cache, which doesn't support bulk removal
        //
        // orphaned keys will be removed by the sliding expiration
        // and the cache reset token
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _cacheResetTokenSource.Cancel();
                _cacheResetTokenSource.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
