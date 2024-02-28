using dig;

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

    private string GetFilePath(string key)
    {
        var safeKey = key.TrimEnd('=').Replace('/', '-').Replace('+', '_');
        return Path.Combine(_cacheDirectory, safeKey).SanitizePath(_cacheDirectory);
    }
}
