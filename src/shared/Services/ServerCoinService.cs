namespace dig;

internal sealed class ServerCoinService(ChiaConfig chiaConfig,
                                        ChiaService chiaService,
                                        ILogger<ServerCoinService> logger,
                                        IConfiguration configuration)
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly ChiaService _chiaService = chiaService;
    private readonly ILogger<ServerCoinService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public void AddServer(string storeId, string serverUtl, ulong mojoReserveAmount, ulong? fee = null)
    {

    }

    public void DeleteServer(string coinId, ulong? fee = null)
    {

    }

    public void GetCoins(string storeId)
    {

    }
}
