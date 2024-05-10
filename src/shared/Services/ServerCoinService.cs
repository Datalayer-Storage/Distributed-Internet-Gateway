using chia.dotnet;
using chia.dotnet.bls;
using chia.dotnet.wallet;
using dig.servercoin;

namespace dig;

public class ServerCoinService(FullNodeProxy fullNode,
                                WalletProxy walletProxy,
                                IObjectStore objectStore,
                                ChiaConfig chiaConfig,
                                ILogger<ServerCoinService> logger) : IServerCoinService, IDisposable
{
    private readonly FullNodeProxy _fullNode = fullNode;
    private readonly WalletProxy _walletProxy = walletProxy;
    private readonly IObjectStore _objectStore = objectStore;
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private StandardWallet? _wallet;
    private readonly ILogger<ServerCoinService> _logger = logger;
    private readonly SemaphoreSlim _walletLock = new(1, 1);
    private bool disposedValue;

    private async Task<ServerCoinFactory> GetFactory(bool sync)
    {
        await _walletLock.WaitAsync();
        try
        {
            if (_wallet is null)
            {
                var keyStore = await KeyStore.CreateFrom(_walletProxy);
                _wallet = new StandardWallet(_fullNode, keyStore);
            }

            if (sync)
            {
                _logger.LogInformation("Syncing wallet...");
                await _wallet.Sync();
            }

            return new ServerCoinFactory(_fullNode, _wallet);
        }
        finally
        {
            _walletLock.Release();
        }
    }

    public async Task<bool> AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    public async Task<ServerCoin> CreateCoin(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee, CancellationToken cancellationToken)
    {
        var factory = await GetFactory(sync: true);
        var spendBundle = await factory.CreateServerCoin(storeId, new[] { new Uri(serverUrl) }, _chiaConfig.GenesisChallenge, mojoReserveAmount, fee, cancellationToken);
        var coin = spendBundle.CoinSpends.First().Coin;
        var serverCoin = new ServerCoin
        {
            Amount = coin.Amount,
            CoinId = coin.CoinId.ToHex(),
            LauncherId = storeId,
            Ours = true,
            Urls = [serverUrl]
        };
        await _objectStore.StoreItem(storeId, serverCoin.CoinId, serverCoin, cancellationToken);

        return serverCoin;
    }

    public async Task<bool> SpendCoin(string storeId, string coinId, ulong fee, CancellationToken cancellationToken)
    {
        var factory = await GetFactory(sync: true);
        await _objectStore.RemoveItem(storeId, coinId, cancellationToken);
        return await factory.DeleteServerCoin(coinId, _chiaConfig.GenesisChallenge, fee, cancellationToken);
    }

    public async Task<IEnumerable<ServerCoin>> GetCoins(string storeId, CancellationToken cancellationToken)
    {
        var factory = await GetFactory(sync: false);
        var coins = await factory.GetServerCoins(storeId, cancellationToken);

        var ourCoins = await _objectStore.GetItems<ServerCoin>(storeId, cancellationToken);
        var coinsDict = coins.ToDictionary(coin => coin.CoinId);

        var newList = new List<ServerCoin>();
        foreach (var coin in coins)
        {
            // copy the coins and set the Ours property
            newList.Add(coin with { Ours = coinsDict.ContainsKey(coin.CoinId) });
        }

        return coins;
    }

    public async Task<IEnumerable<ServerCoin>> SyncCoins(string storeId, CancellationToken cancellationToken)
    {
        var factory = await GetFactory(sync: true);
        var coins = await factory.GetServerCoins(storeId, cancellationToken);
        foreach (var coin in coins)
        {
            await _objectStore.StoreItem(storeId, coin.CoinId, coin, cancellationToken);
        }
        return coins;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _walletLock.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
