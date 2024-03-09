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

    public async Task<string?> GetValueAsync(string key, CancellationToken token)
    {
        var filePath = GetFilePath(key).SanitizePath(_cacheDirectory);
        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath, token);
        }

        return null;
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

            return item.ToObject<TItem>();
        }

        return default;
    }

    public async Task SetValueAsync(string key, object? value, CancellationToken token)
    {
        if (value is not null)
        {
            var filePath = GetFilePath(key).SanitizePath(_cacheDirectory);
            if (value is string stringValue)
            {
                await File.WriteAllTextAsync(filePath, stringValue, token);
            }
            else
            {
                await File.WriteAllTextAsync(filePath, value.ToJson(), token);
            }

            _logger.LogWarning("Cached {Key} of type {Type}", key, value.GetType().Name);
        }
    }

    public async Task SetValueAsync(string key, string value, CancellationToken token)
    {
        var filePath = GetFilePath(key).SanitizePath(_cacheDirectory);
        await File.WriteAllTextAsync(filePath, value, token);
    }

    public void InvalidateAllCache()
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

    public void InvalidateStore(string storeId, Func<string, Task> callback)
    {
        _logger.LogInformation("Invalidating store {storeId}", storeId);
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, $"{storeId}*"))
            {
                var sanitizedPath = file.SanitizePath(_cacheDirectory);
                _logger.LogInformation("Deleting file {File}", sanitizedPath);
                File.Delete(sanitizedPath);

                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sanitizedPath);
                callback(fileNameWithoutExtension);
            }
        }
    }

    private string GetFilePath(string key) => Path.Combine(_cacheDirectory, key);
}
