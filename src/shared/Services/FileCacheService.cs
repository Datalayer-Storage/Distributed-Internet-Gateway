using chia.dotnet;

namespace dig;

public class FileCacheService
{
    private readonly string _cacheDirectory;
    private readonly ILogger<FileCacheService> _logger;

    public FileCacheService(AppStorage appStorage, ILogger<FileCacheService> logger)
    {
        _logger = logger;
        _cacheDirectory = Path.Combine(appStorage.UserSettingsFolder, "store-cache");
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public async Task<TItem?> GetOrCreateAsync<TItem>(string key, Func<Task<TItem>> factory, CancellationToken token)
    {
        var fileValue = await GetValueAsync<TItem>(key, token);

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
            await SetValueAsync(key, newValue, token);
        }

        return newValue;
    }

    public async Task<TItem?> GetValueAsync<TItem>(string key, CancellationToken token)
    {
        var filePath = GetFilePath(key).SanitizePath(_cacheDirectory);
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

    public async Task SetValueAsync<TItem>(string key, TItem? value, CancellationToken token)
    {
        if (value is not null)
        {
            var filePath = GetFilePath(key).SanitizePath(_cacheDirectory);
            if (typeof(TItem) == typeof(string))
            {
                await File.WriteAllTextAsync(filePath, value.ToString(), token);
            }
            else
            {
                await File.WriteAllTextAsync(filePath, value.ToJson(), token);
            }

            _logger.LogWarning("Cached {Key} of type {Type}", key, typeof(TItem).Name);
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
        }
    }

    public void RemoveStore(string storeId)
    {
        _logger.LogInformation("Invalidating store {storeId}", storeId);
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, $"{storeId}*"))
            {
                var sanitizedPath = file.SanitizePath(_cacheDirectory);
                _logger.LogInformation("Deleting file {File}", sanitizedPath);
                File.Delete(sanitizedPath);
            }
        }
    }

    public void RemoveKey(string key)
    {
        var filePath = GetFilePath(key).SanitizePath(_cacheDirectory);
        if (File.Exists(filePath))
        {
            _logger.LogInformation("Deleting file {File}", filePath);
            File.Delete(filePath);
        }
    }

    private string GetFilePath(string key) => Path.Combine(_cacheDirectory, key);
}
