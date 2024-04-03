
namespace dig;

public interface IServerCoinService
{
    bool AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee);
    string DeleteServer(string storeId, string coinId, ulong fee);
    IEnumerable<ServerCoin> GetCoins(string storeId);
}
