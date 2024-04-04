
namespace dig;

using dig.servercoin;

public interface IServerCoinService
{
    Task<bool> AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee);
    Task<bool> DeleteServer(string storeId, string coinId, ulong fee);
    Task<IEnumerable<ServerCoin>> GetCoins(string storeId);
}
