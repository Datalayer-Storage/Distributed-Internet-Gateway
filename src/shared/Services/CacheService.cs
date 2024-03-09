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
            return await _fileCache.GetOrCreateAsync(key, factory, token);
        });
    }

    public async Task<TItem?> GetOrCreateAsync<TItem>(string key, DateTimeOffset expiresOn, Func<Task<TItem>> factory, CancellationToken token)
    {
        return await _memoryCache.GetOrCreateAsync(key, async entry =>
        {
            _logger.LogWarning("Memory cache miss for {key}", key.SanitizeForLog());

            entry.AbsoluteExpiration = expiresOn;

            // this ensures that the value is in the file cache and then adds it to the memory cache
            return await _fileCache.GetOrCreateAsync(key, factory, token);
        });
    }
}
