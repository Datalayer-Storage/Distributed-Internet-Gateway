namespace dig;

using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

public class ServerCoinService(ChiaConfig chiaConfig,
                                ILogger<ServerCoinService> logger,
                                IConfiguration configuration) : IServerCoinService
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly ILogger<ServerCoinService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public bool AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee)
    {
        return false;
    }

    public string DeleteServer(string storeId, string coinId, ulong fee)
    {
        return "";
    }
    
    public IEnumerable<ServerCoin> GetCoins(string storeId)
    {
        return [];
    }
}
