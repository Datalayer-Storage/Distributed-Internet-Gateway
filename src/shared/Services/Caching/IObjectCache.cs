namespace dig.caching;

public interface IObjectCache
{
    Task<TItem?> GetOrCreateAsync<TItem>(string topic, string objectId, string rootHash, string key, Func<Task<TItem>> factory, CancellationToken token);
    Task<TItem?> GetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, CancellationToken token);
    Task SetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, TItem? value, CancellationToken token);
    void RemoveStore(string topic, string objectId);
    void Clear();
    void Clear(string topic);
}
