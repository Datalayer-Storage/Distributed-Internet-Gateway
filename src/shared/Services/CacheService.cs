using Microsoft.Extensions.Caching.Memory;

namespace dig;

public class CacheService(IMemoryCache memoryCache,
                            FileCacheService fileCache,
                            ILogger<CacheService> logger)
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly FileCacheService _fileCache = fileCache;
    private readonly ILogger<CacheService> _logger = logger;

    public async Task<TItem?> GetOrCreateAsync<TItem>(string key, TimeSpan slidingExpiry, Func<Task<TItem>> factory, CancellationToken token)
    {
        return await _memoryCache.GetOrCreateAsync(key, async entry =>
        {
            _logger.LogWarning("Memory cache miss for {key}", key.SanitizeForLog());

            entry.SlidingExpiration = slidingExpiry;

            // this ensures that the value is in the file cache and then adds it to the memory cache
            return await GetOrCreateFromFileCache(key, factory, token);
        });
    }

    public async Task<TItem?> GetOrCreateAsync<TItem>(string key, DateTimeOffset expiresOn, Func<Task<TItem>> factory, CancellationToken token)
    {
        // the value is in the memory cache so return it
        return await _memoryCache.GetOrCreateAsync(key, async entry =>
        {
            _logger.LogWarning("Memory cache miss for {key}", key.SanitizeForLog());

            entry.AbsoluteExpiration = expiresOn;

            // this ensures that the value is in the file cache and then adds it to the memory cache
            return await GetOrCreateFromFileCache(key, factory, token);
        });
    }

    private async Task<TItem?> GetOrCreateFromFileCache<TItem>(string key, Func<Task<TItem>> factory, CancellationToken token)
    {
        var fileValue = await _fileCache.GetValueAsync<TItem>(key, token);

        // the value is in the file cache, add it to the memory cache and return it
        if (fileValue is not null)
        {
            return fileValue;
        }

        // it's not cached so create it and add it to the file cache if it's not null
        _logger.LogWarning("File cache miss for {key}", key.SanitizeForLog());
        var newValue = await factory();

        if (newValue is not null)
        {
            await _fileCache.SetValueAsync(key, newValue, token);
        }

        return newValue;
    }
}
