using dig.servercoin;

namespace dig;

public interface IServerCoinService
{
    Task<bool> AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee, CancellationToken token);
    Task<bool> DeleteServer(string storeId, string coinId, ulong fee, CancellationToken token);
    Task<IEnumerable<ServerCoin>> GetCoins(string storeId, CancellationToken token);
}
