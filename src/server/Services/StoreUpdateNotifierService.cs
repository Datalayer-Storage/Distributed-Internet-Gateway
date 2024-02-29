
using chia.dotnet;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace dig.server;

public class StoreUpdateNotifierService : IDisposable
{
    private readonly DataLayerProxy _dataLayer;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger _logger;
    private readonly List<Func<string, Task>> _callbacks = [];
    private Timer? _timer;
    private bool disposedValue;
    private readonly string _cacheDirectory;
    private readonly FileCacheService _fileCache;
    private readonly ConcurrentDictionary<string, string> _storeIds = new();
    private readonly ConcurrentQueue<string> _preCacheQueue = new ConcurrentQueue<string>();
    private readonly SemaphoreSlim _preCacheLock = new SemaphoreSlim(1, 1);

    public StoreUpdateNotifierService(DataLayerProxy dataLayer, IMemoryCache memoryCache, ILogger logger, FileCacheService fileCache)
    {
        _dataLayer = dataLayer;
        _memoryCache = memoryCache;
        _logger = logger;
        _fileCache = fileCache;
    }

    public async Task RegisterStoreAsync(string storeId)
    {
        try
        {
            var cacheKey = $"root_hash_{storeId}";
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                _logger.LogInformation("StoreId {storeId} already exists in memory cache.", storeId.SanitizeForLog());
                return;
            }

            var rootHash = await _dataLayer.GetRoot(storeId, default);
            _logger.LogInformation("Retrieved root hash {rootHash} for storeId {storeId}.", rootHash.Hash, storeId.SanitizeForLog());

            if (rootHash != null)
            {
                _logger.LogInformation("Retrieved root hash {rootHash} for storeId {storeId}.", rootHash.Hash, storeId.SanitizeForLog());
                _memoryCache.Set(cacheKey, rootHash.Hash);
                _storeIds.TryAdd(storeId, cacheKey); // Track the store ID

                var filePath = Path.Combine(_cacheDirectory, $"{storeId}-root_hash").SanitizePath(_cacheDirectory);
                await File.WriteAllTextAsync(filePath, rootHash.Hash);

                _logger.LogInformation("Stored root hash for storeId {storeId} successfully.", storeId.SanitizeForLog());
            }
            else
            {
                _logger.LogError("Failed to retrieve root hash for storeId {storeId}.", storeId.SanitizeForLog());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting root hash to cache for storeId {storeId}.", storeId.SanitizeForLog());
        }
    }

    public async Task ProcessPreCacheQueueAsync()
    {
        // Wait to enter the semaphore. If no one is inside, enter immediately.
        // Otherwise, wait until the semaphore is released.
        await _preCacheLock.WaitAsync();

        try
        {
            while (_preCacheQueue.TryDequeue(out var storeId))
            {
                _logger.LogInformation($"Processing pre-cache for storeId: {storeId} on a separate thread.");

                // Execute PreCacheStore on a separate thread and wait for it to complete
                await Task.Run(async () => await PreCacheStore(storeId));
            }
        }
        finally
        {
            // When the task is complete, release the semaphore so another instance can run
            _preCacheLock.Release();
        }
    }

    public async Task PreCacheStore(string storeId)
    {
        var lastRootHash = await _dataLayer.GetRoot(storeId, CancellationToken.None);
        var storeKeys = await _dataLayer.GetKeys(storeId, lastRootHash.Hash, CancellationToken.None);
        if (storeKeys != null)
        {
            foreach (var key in storeKeys)
            {
                _logger.LogInformation("Precaching key {key} for storeId {storeId}.", key.SanitizeForLog(), storeId.SanitizeForLog());
                var value = await _dataLayer.GetValue(storeId, key, lastRootHash.Hash, CancellationToken.None);
                if (value != null)
                {
                    var cacheKey = $"{storeId}-{key}";
                    await _fileCache.SetValueAsync(cacheKey, value);
                }
                await Task.Delay(500);
            }
        }
    }

    public async Task UnregisterStoreAsync(string storeId)
    {
        await Task.CompletedTask;

        var cacheKey = $"root_hash_{storeId}";
        _memoryCache.Remove(cacheKey);

        _storeIds.TryRemove(storeId, out _); // Remove the store ID from tracking

        var filePath = Path.Combine(_cacheDirectory, $"{storeId}-root_hash").SanitizePath(_cacheDirectory);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Successfully unwatched and deleted cache for storeId {storeId}.", storeId.SanitizeForLog());
        }
        else
        {
            _logger.LogInformation("No root_hash file found for storeId {storeId} to unwatch.", storeId.SanitizeForLog());
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
                _logger.LogInformation("Updated in-memory cache for {storeId} with new root hash.", storeId.SanitizeForLog());

                var filePath = Path.Combine(_cacheDirectory, $"{storeId}-root_hash").SanitizePath(_cacheDirectory);
                await File.WriteAllTextAsync(filePath, newRootHash.Hash);

                _logger.LogInformation("Updated file cache for {storeId} with new root hash.", storeId.SanitizeForLog());

                foreach (var callback in _callbacks)
                {
                    _logger.LogInformation("Invoking callback for storeId {storeId}.", storeId.SanitizeForLog());
                    await callback(storeId);
                }

                _preCacheQueue.Enqueue(storeId);
                // purposely not awaited - fire and forget
                ProcessPreCacheQueueAsync();
            }
        }
    }

    public void StartWatcher(Func<string, Task> callback, TimeSpan interval)
    {
        Debug.Assert(_timer == null, "Watcher already started");
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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _callbacks.Clear();
                _timer?.Change(Timeout.Infinite, 0);
                _timer?.Dispose();
                _timer = null;
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
