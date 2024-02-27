public class FileCacheService
{
    private readonly string _cacheDirectory;

    public FileCacheService(string cacheDirectory)
    {
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

    public void Invalidate(string storeId)
    {
        var storeDirectory = Path.Combine(_cacheDirectory, storeId);
        if (Directory.Exists(storeDirectory))
        {
            Directory.Delete(storeDirectory, true);
        }
    }

    private string GetFilePath(string key)
    {
        var safeKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key)).TrimEnd('=').Replace('/', '-').Replace('+', '_');
        return Path.Combine(_cacheDirectory, safeKey);
    }
}
