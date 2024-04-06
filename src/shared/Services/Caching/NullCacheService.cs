namespace dig.caching;

/// <summary>
/// The cache service that does not actually cache anything. Used to disable caching altogether.
/// </summary>
public class NullCacheService : IObjectCache
{
    public async Task<TItem?> GetOrCreateAsync<TItem>(string topic, string objectId, string rootHash, string key, Func<Task<TItem>> factory, CancellationToken token) => await factory();

    public async Task<TItem?> GetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, CancellationToken token) => await Task.FromResult(default(TItem));

    public async Task SetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, TItem? value, CancellationToken token) => await Task.CompletedTask;

    public void Clear()
    {
    }
    
    public void Clear(string topic)
    {
    }

    public void RemoveValue(string topic, string objectKey)
    {
    }
}
