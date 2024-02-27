
using chia.dotnet;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace dig.server
{
    public class StoreUpdateNotifierService
    {
        private readonly DataLayerProxy _dataLayer;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger _logger;
        private readonly List<Func<string, Task>> _callbacks = new();
        private Timer _timer;
        private readonly string _cacheDirectory;
        private readonly ConcurrentDictionary<string, string> _storeIds = new ConcurrentDictionary<string, string>();

        public StoreUpdateNotifierService(DataLayerProxy dataLayer, IMemoryCache memoryCache, ILogger logger)
        {
            _dataLayer = dataLayer;
            _memoryCache = memoryCache;
            _logger = logger;
            _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoreUpdateNotifierCache");
            Directory.CreateDirectory(_cacheDirectory); // Ensure the cache directory exists
        }

        public async Task RegisterStoreAsync(string storeId)
        {
            try
            {
                var cacheKey = $"root_hash_{storeId}";
                if (_memoryCache.TryGetValue(cacheKey, out _))
                {
                    _logger.LogInformation($"StoreId {storeId} already exists in memory cache.");
                    return;
                }

                var rootHash = await _dataLayer.GetRoot(storeId, default);
                _logger.LogInformation($"Retrieved root hash {rootHash.Hash} for storeId {storeId}.");

                if (rootHash != null)
                {
                    _logger.LogInformation($"Retrieved root hash {rootHash.Hash} for storeId {storeId}.");
                    _memoryCache.Set(cacheKey, rootHash.Hash);
                    _storeIds.TryAdd(storeId, cacheKey); // Track the store ID

                    var filePath = Path.Combine(_cacheDirectory, $"{storeId}-root_hash");
                    await File.WriteAllTextAsync(filePath, rootHash.Hash);

                    _logger.LogInformation($"Stored root hash for storeId {storeId} successfully.");
                }
                else
                {
                    _logger.LogError($"Failed to retrieve root hash for storeId {storeId}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting root hash to cache for storeId {storeId}.");
            }
        }

        public async Task UnregisterStoreAsync(string storeId)
        {
            var cacheKey = $"root_hash_{storeId}";
            _memoryCache.Remove(cacheKey);

            _storeIds.TryRemove(storeId, out _); // Remove the store ID from tracking

            var filePath = Path.Combine(_cacheDirectory, $"{storeId}-root_hash");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation($"Successfully unwatched and deleted cache for storeId {storeId}.");
            }
            else
            {
                _logger.LogInformation($"No root_hash file found for storeId {storeId} to unwatch.");
            }
        }

        public async Task LoadRootHashesToCacheAsync()
        {
            var files = Directory.GetFiles(_cacheDirectory, "*-root_hash");
            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                var storeId = Path.GetFileNameWithoutExtension(file).Split("-root_hash")[0];
                var cacheKey = $"root_hash_{storeId}";
                _memoryCache.Set(cacheKey, content);
                _storeIds.TryAdd(storeId, cacheKey); // Ensure the store ID is tracked
            }
        }

        public async Task RefreshRootHashesAsync()
        {
            foreach (var storeId in _storeIds.Keys) // Iterate over tracked store IDs
            {
                var cacheKey = _storeIds[storeId];
                var currentRootHash = _memoryCache.Get<string>(cacheKey);

                var newRootHash = await _dataLayer.GetRoot(storeId, CancellationToken.None);

                if (newRootHash != null && !newRootHash.Hash.Equals(currentRootHash))
                {
                    _memoryCache.Set(cacheKey, newRootHash.Hash);
                    _logger.LogInformation($"Updated in-memory cache for {storeId} with new root hash.");

                    var filePath = Path.Combine(_cacheDirectory, $"{storeId}-root_hash");
                    await File.WriteAllTextAsync(filePath, newRootHash.Hash);

                    _logger.LogInformation($"Updated file cache for {storeId} with new root hash.");

                    foreach (var callback in _callbacks)
                    {
                        _logger.LogInformation($"Invoking callback for storeId {storeId}.");
                        await callback(storeId);
                    }
                }
            }
        }

        public void StartWatcher(Func<string, Task> callback, TimeSpan interval)
        {
            if (_callbacks.Count == 0)
            {
                _timer = new Timer(async _ => await RefreshRootHashesAsync(), null, TimeSpan.Zero, interval);
            }
            _callbacks.Add(callback);
        }

        public void StopWatcher(Func<string, Task> callback)
        {
            _callbacks.Remove(callback);
            if (_callbacks.Count == 0)
            {
                _timer?.Change(Timeout.Infinite, 0);
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
