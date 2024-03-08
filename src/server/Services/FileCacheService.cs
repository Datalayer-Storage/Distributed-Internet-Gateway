using System.Security.Cryptography;
using System.Text;
namespace dig.server;

public class FileCacheService
{
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;

    public FileCacheService(string cacheDirectory, ILogger logger, bool Invalidate = false)
    {
        _logger = logger;
        _cacheDirectory = cacheDirectory;
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }

        if (Invalidate)
        {
            // If the DIG node is just starting up, we want to clear the cache
            // Because it could be super stale
            InvalidateAllCache();
        }

    }

    public async Task<string?> GetValueAsync(string key, CancellationToken token)
    {
        string keyHash = ComputeMd5Hash(key);
        var filePath = GetFilePath(keyHash).SanitizePath(_cacheDirectory);
        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath, token);
        }

        return null;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken token)
    {
        // hash the key so we don't have to worry about file path length exceeding the max
        string keyHash = ComputeMd5Hash(key);
        var filePath = GetFilePath(keyHash).SanitizePath(_cacheDirectory);
        await File.WriteAllTextAsync(filePath, value, token);
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

    private string GetFilePath(string key) => Path.Combine(_cacheDirectory, key);

    public string ComputeMd5Hash(string input)
    {
        return string.Concat(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input)).Select(x => x.ToString("x2")));
    }
}
