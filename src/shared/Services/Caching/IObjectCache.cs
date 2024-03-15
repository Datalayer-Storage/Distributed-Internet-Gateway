namespace dig.caching;

public interface IObjectCache
{
    Task<TItem?> GetOrCreateAsync<TItem>(string storeId, string rootHash, string key, Func<Task<TItem>> factory, CancellationToken token);
    Task<TItem?> GetValueAsync<TItem>(string storeId, string rootHash, string key, CancellationToken token);
    Task SetValueAsync<TItem>(string storeId, string rootHash, string key, TItem? value, CancellationToken token);
    void RemoveStore(string storeId);
    void Clear();
}
