namespace dig.server;

public class FileCacheService
{
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;

    public FileCacheService(string cacheDirectory, ILogger logger)
    {
        _logger = logger;
        _cacheDirectory = cacheDirectory;
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
        
        // If the DIG node is just starting up, we want to clear the cache
        // Because it could be super stale
        InvalidateAllCache();
    }

    public async Task<string?> GetValueAsync(string key)
    {
        var filePath = GetFilePath(key);
        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath);
        }

        return null;
    }

    public async Task SetValueAsync(string key, string value)
    {
        var filePath = GetFilePath(key);
        await File.WriteAllTextAsync(filePath, value);
    }

    public void InvalidateAllCache()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, $"*"))
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

    private string GetFilePath(string key) => Path.Combine(_cacheDirectory, key).SanitizePath(_cacheDirectory);
}
