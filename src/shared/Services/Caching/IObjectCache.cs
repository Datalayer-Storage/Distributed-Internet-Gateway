namespace dig.caching;

public interface IObjectCache
{
    Task<TItem?> GetOrCreateAsync<TItem>(string topic, string objectId, string rootHash, string key, Func<Task<TItem>> factory, CancellationToken token);
    Task<TItem?> GetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, CancellationToken token);
    Task SetValueAsync<TItem>(string topic, string objectId, string rootHash, string key, TItem? value, CancellationToken token);
    void RemoveValue(string topic, string objectKey);
    void Clear();
    void Clear(string topic);
}
