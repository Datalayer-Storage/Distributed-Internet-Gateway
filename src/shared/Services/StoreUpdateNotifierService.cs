
using chia.dotnet;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace dig;

public class StoreUpdateNotifierService(DataLayerProxy dataLayer,
                                        IMemoryCache memoryCache,
                                        FileCacheService fileCache,
                                        ILogger<StoreUpdateNotifierService> logger) : IDisposable
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly FileCacheService _fileCache = fileCache;
    private readonly ILogger<StoreUpdateNotifierService> _logger = logger;
    private readonly List<Func<string, Task>> _callbacks = [];
    private Timer? _timer;
    private bool disposedValue;
    private readonly ConcurrentDictionary<string, string> _storeIds = new();
    private readonly ConcurrentQueue<string> _preCacheQueue = new();
    private readonly SemaphoreSlim _preCacheLock = new(1, 1);

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

    public async Task ProcessPreCacheQueueAsync(CancellationToken cancellationToken)
    {
        // Wait to enter the semaphore. If no one is inside, enter immediately.
        // Otherwise, wait until the semaphore is released.
        await _preCacheLock.WaitAsync(cancellationToken);

        try
        {
            while (_preCacheQueue.TryDequeue(out var storeId))
            {
                _logger.LogInformation("Processing pre-cache for storeId: {storeId} on a separate thread.", storeId);

                // Execute PreCacheStore on a separate thread and wait for it to complete
                await Task.Run(async () => await PreCacheStore(storeId, cancellationToken), cancellationToken);
            }
        }
        finally
        {
            // When the task is complete, release the semaphore so another instance can run
            _preCacheLock.Release();
        }
    }

    public async Task PreCacheStore(string storeId, CancellationToken cancellationToken)
    {
        var lastRootHash = await _dataLayer.GetRoot(storeId, cancellationToken);
        var storeKeys = await _dataLayer.GetKeys(storeId, lastRootHash.Hash, cancellationToken);
        if (storeKeys != null)
        {
            foreach (var key in storeKeys)
            {
                _logger.LogInformation("Pre-caching key {key} for storeId {storeId}.", key.SanitizeForLog(), storeId.SanitizeForLog());
                var value = await _dataLayer.GetValue(storeId, key, lastRootHash.Hash, cancellationToken);
                if (value != null)
                {
                    var cacheKey = $"{storeId}-{key}";
                    await _fileCache.SetValueAsync(cacheKey, value, cancellationToken);
                }

                await Task.Delay(500, cancellationToken);
            }
        }
    }

    public async Task UnregisterStoreAsync(string storeId)
    {
        await Task.CompletedTask;

        var cacheKey = $"root_hash_{storeId}";
        _memoryCache.Remove(cacheKey);

        _storeIds.TryRemove(storeId, out _); // Remove the store ID from tracking
    }

    public async Task RefreshRootHashesAsync(CancellationToken cancellationToken)
    {
        foreach (var storeId in _storeIds.Keys) // Iterate over tracked store IDs
        {
            var cacheKey = _storeIds[storeId];
            var currentRootHash = _memoryCache.Get<string>(cacheKey);
            var newRootHash = await _dataLayer.GetRoot(storeId, cancellationToken);

            if (newRootHash != null && !newRootHash.Hash.Equals(currentRootHash))
            {
                _memoryCache.Set(cacheKey, newRootHash.Hash);
                _logger.LogInformation("Updated memory cache for {storeId} with new root hash.", storeId.SanitizeForLog());

                foreach (var callback in _callbacks)
                {
                    _logger.LogInformation("Invoking callback for storeId {storeId}.", storeId.SanitizeForLog());
                    await callback(storeId);
                }

                _preCacheQueue.Enqueue(storeId);
                // purposely not awaited - fire and forget
                _ = ProcessPreCacheQueueAsync(CancellationToken.None);
            }
        }
    }

    public void StartWatcher(Func<string, Task> callback, TimeSpan interval, CancellationToken cancellationToken)
    {
        Debug.Assert(_timer == null, "Watcher already started");
        if (_callbacks.Count == 0)
        {
            _timer = new Timer(async _ => await RefreshRootHashesAsync(cancellationToken), null, TimeSpan.Zero, interval);
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
