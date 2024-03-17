using chia.dotnet;

namespace dig.caching;

public class FileCacheService : IObjectCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<FileCacheService> _logger;
    private readonly IConfiguration _configuration;

    public FileCacheService(IConfiguration configuration, ILogger<FileCacheService> logger)
    {
        _logger = logger;
        _configuration = configuration;

        _cacheDirectory = _configuration.GetValue("dig:FileCacheDirectory", "") ?? throw new InvalidOperationException("Cache directory not found in configuration");
        if (string.IsNullOrEmpty(_cacheDirectory))
        {
            throw new InvalidOperationException("Cache directory not found in configuration");
        }

        _cacheDirectory = Environment.ExpandEnvironmentVariables(_cacheDirectory);
        if (!Path.IsPathFullyQualified(_cacheDirectory))
        {
            throw new InvalidOperationException($"Cache directory path is not fully qualified: {_cacheDirectory}");
        }

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public async Task<TItem?> GetOrCreateAsync<TItem>(string storeId, string rootHash, string key, Func<Task<TItem>> factory, CancellationToken token)
    {
        var fileValue = await GetValueAsync<TItem>(storeId, rootHash, key, token);

        // the value is in the file cache just return it
        if (fileValue is not null)
        {
            return fileValue;
        }

        // it's not cached so create it and add it to the file cache if it's not null
        _logger.LogWarning("File cache miss for {key}", key.SanitizeForLog());
        var newValue = await factory();

        if (newValue is not null)
        {
            await SetValueAsync(storeId, rootHash, key, newValue, token);
        }

        return newValue;
    }

    public async Task<TItem?> GetValueAsync<TItem>(string storeId, string rootHash, string key, CancellationToken token)
    {
        var filePath = GetFilePath(storeId, rootHash, key).SanitizePath(_cacheDirectory);
        if (File.Exists(filePath))
        {
            var item = await File.ReadAllTextAsync(filePath, token);

            if (typeof(TItem) == typeof(string))
            {
                return (TItem)(object)item; //coerce the string to TItem
            }

            // otherwise assume it's json and deserialize it
            return item.ToObject<TItem>();
        }

        return default;
    }

    public async Task SetValueAsync<TItem>(string storeId, string rootHash, string key, TItem? value, CancellationToken token)
    {
        if (value is not null)
        {
            var filePath = GetFilePath(storeId, rootHash, key).SanitizePath(_cacheDirectory);
            if (typeof(TItem) == typeof(string))
            {
                await File.WriteAllTextAsync(filePath, value.ToString(), token);
            }
            else
            {
                await File.WriteAllTextAsync(filePath, value.ToJson(), token);
            }

            _logger.LogInformation("Cached {Key} of type {Type}", key, typeof(TItem).Name);
        }
    }

    public void Clear()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, "*"))
            {
                var sanitizedPath = file.SanitizePath(_cacheDirectory);
                _logger.LogInformation("Deleting file {File}", sanitizedPath);
                File.Delete(sanitizedPath);
            }

            foreach (var dir in Directory.GetDirectories(_cacheDirectory))
            {
                var sanitizedPath = dir.SanitizePath(_cacheDirectory);
                _logger.LogInformation("Deleting directory {Directory}", sanitizedPath);
                Directory.Delete(sanitizedPath, true);
            }
        }
    }

    public void RemoveStore(string storeId)
    {
        _logger.LogWarning("Invalidating store {storeId}", storeId);
        var storeCacheDirectory = Path.Combine(_cacheDirectory, storeId).SanitizePath(_cacheDirectory);

        if (Directory.Exists(storeCacheDirectory))
        {
            Directory.Delete(storeCacheDirectory, true);
        }
    }

    private string GetFilePath(string storeId, string rootHash, string key)
    {
        var storeCacheDirectory = Path.Combine(_cacheDirectory, storeId).SanitizePath(_cacheDirectory);
        if (!Directory.Exists(storeCacheDirectory))
        {
            Directory.CreateDirectory(storeCacheDirectory);
        }

        var combinedKey = string.Concat(rootHash, key);
        return Path.Combine(storeCacheDirectory, combinedKey.MD5Hash());
    }
}
