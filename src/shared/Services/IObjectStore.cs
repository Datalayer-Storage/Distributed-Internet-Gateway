namespace dig;

public interface IObjectStore
{
    Task<TItem?> GetItem<TItem>(string folder, string objectId, CancellationToken token);
    Task StoreItem<TItem>(string folder, string objectId, TItem item, CancellationToken token);
    Task<IEnumerable<TItem>> GetItems<TItem>(string folder, CancellationToken token);
    Task RemoveItem(string folder, string objectId, CancellationToken token);
}
