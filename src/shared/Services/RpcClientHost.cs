using chia.dotnet;

/// <summary>
/// Wrapper type to hold multiple RpcClient instances by name and dispose of them
/// It owns the RpcClient lifetimes so don't dispose when retrieved
/// </summary>
internal sealed class RpcClientHost(ChiaConfig chiaConfig) : IDisposable
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly Dictionary<string, HttpRpcClient> _rpcClients = [];

    public HttpRpcClient GetRpcClient(string name)
    {
        // if we haven't cached an rpc client for this endpoint, do so now
        if (!_rpcClients.TryGetValue(name, out HttpRpcClient? value))
        {
            var endpoint = _chiaConfig.GetEndpoint(name);
            value = new HttpRpcClient(endpoint);
            _rpcClients.Add(name, value);
        }
        return value;
    }

    public void Dispose()
    {
        foreach (var rpcClient in _rpcClients.Values)
        {
            rpcClient.Dispose();
        }
    }
}
