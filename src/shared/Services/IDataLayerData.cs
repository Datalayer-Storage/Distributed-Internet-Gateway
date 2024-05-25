namespace dig;

/// <summary>
/// Because the data layer RPC has size limitations, we use this interface to swap in
/// alternative data layer implementations such as going after the CLI directly.
/// </summary>
public interface IDataLayerData
{
    Task<string> GetValue(string storeId, string key, string? rootHash, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetKeys(string storeId, string? rootHash, CancellationToken cancellationToken = default);
}
