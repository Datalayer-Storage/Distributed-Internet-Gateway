
namespace dig;

public interface IServerCoinService
{
    Task<bool> AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee);
    Task<string> DeleteServer(string storeId, string coinId, ulong fee);
    Task<IEnumerable<ServerCoin>> GetCoins(string storeId);
}
