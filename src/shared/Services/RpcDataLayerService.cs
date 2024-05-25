using chia.dotnet;

namespace dig;

public class RpcDataLayerService(DataLayerProxy dataLayerProxy, ILogger<RpcDataLayerService> logger) : IDataLayerData
{
    private readonly DataLayerProxy _dataLayerProxy = dataLayerProxy;
    private readonly ILogger<RpcDataLayerService> _logger = logger;

    public async Task<string> GetValue(string storeId, string key, string? rootHash, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting value for {StoreId} {Key}", storeId.SanitizeForLog(), key.SanitizeForLog());

        return await _dataLayerProxy.GetValue(storeId, key, rootHash, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetKeys(string storeId, string? rootHash, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting keys for {StoreId}", storeId.SanitizeForLog());

        return await _dataLayerProxy.GetKeys(storeId, rootHash, cancellationToken);
    }
}
