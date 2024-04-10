using chia.dotnet;

namespace dig;

public sealed class FileObjectStore : IObjectStore
{
    private readonly string _storeDirectory;
    private readonly ILogger<FileObjectStore> _logger;
    private readonly IConfiguration _configuration;

    public FileObjectStore(IConfiguration configuration, ILogger<FileObjectStore> logger)
    {
        _logger = logger;
        _configuration = configuration;

        _storeDirectory = _configuration.GetValue("dig:ObjectStoreDirectory", "") ?? throw new InvalidOperationException("Store directory not found in configuration");
        if (string.IsNullOrEmpty(_storeDirectory))
        {
            throw new InvalidOperationException("Store directory not found in configuration");
        }

        _storeDirectory = Environment.ExpandEnvironmentVariables(_storeDirectory);
        if (!Path.IsPathFullyQualified(_storeDirectory))
        {
            throw new InvalidOperationException($"Cache directory path is not fully qualified: {_storeDirectory}");
        }
    }

    public async Task<TItem?> GetItem<TItem>(string folder, string objectId, CancellationToken token)
    {
        var filePath = GetObjectPath(folder, objectId);
        if (File.Exists(filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, token);
                return json.ToObject<TItem>();
            }
            catch
            {
                _logger.LogWarning("Failed to get item {objectId} from {folder}", objectId.SanitizeForLog(), folder.SanitizeForLog());
            }
        }
        return default;
    }

    public async Task StoreItem<TItem>(string folder, string objectId, TItem item, CancellationToken token)
    {
        if (item is not null)
        {
            _logger.LogInformation("Storing item {objectId} in {folder}", objectId.SanitizeForLog(), folder.SanitizeForLog());

            var filePath = MakeDirectory(folder, objectId);
            var json = item.ToJson();
            await File.WriteAllTextAsync(filePath, json, token);
        }
        else
        {
            _logger.LogWarning("Attempted to store null item {objectId} in {folder}", objectId.SanitizeForLog(), folder.SanitizeForLog());
        }
    }

    public async Task<IEnumerable<TItem>> GetItems<TItem>(string folder, CancellationToken token)
    {
        var items = new List<TItem>();
        var itemsDirectory = Path.Combine(_storeDirectory, folder).SanitizePath(_storeDirectory);
        if (Directory.Exists(itemsDirectory))
        {
            try
            {
                var files = await Task.Run(() => Directory.EnumerateFiles(itemsDirectory, "*", SearchOption.TopDirectoryOnly), token);
                foreach (var file in files)
                {
                    var json = await File.ReadAllTextAsync(file, token);
                    var item = json.ToObject<TItem>();
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
            }
            catch
            {
                _logger.LogWarning("Failed to get items from {folder}", folder.SanitizeForLog());
            }
        }

        return items;
    }

    public async Task RemoveItem(string folder, string objectId, CancellationToken token)
    {
        var filePath = GetObjectPath(folder, objectId);
        if (File.Exists(filePath))
        {
            try
            {
                // this locks the file
                using var fileStream = new FileStream(filePath, FileMode.Truncate, FileAccess.Write, FileShare.Delete, 4096, true);
                await fileStream.FlushAsync(token);
                File.Delete(filePath);
            }
            catch
            {
                _logger.LogWarning("Failed to remove item {objectId} from {folder}", objectId.SanitizeForLog(), folder.SanitizeForLog());
            }
        }
    }

    private string GetObjectPath(string folder, string objectId) => Path.Combine(_storeDirectory, folder, objectId).SanitizePath(_storeDirectory);

    private string MakeDirectory(string folder, string objectId)
    {
        var storeCacheDirectory = Path.Combine(_storeDirectory, folder).SanitizePath(_storeDirectory);
        if (!Directory.Exists(storeCacheDirectory))
        {
            Directory.CreateDirectory(storeCacheDirectory);
        }

        return Path.Combine(storeCacheDirectory, objectId);
    }
}
