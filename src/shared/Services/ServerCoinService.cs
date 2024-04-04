using chia.dotnet;
using chia.dotnet.wallet;
using dig.servercoin;

namespace dig;

public class ServerCoinService(FullNodeProxy fullNode,
                                WalletProxy walletProxy,
                                ILogger<ServerCoinService> logger) : IServerCoinService, IDisposable
{
    private readonly FullNodeProxy _fullNode = fullNode;
    private readonly WalletProxy _walletProxy = walletProxy;
    private StandardWallet? _wallet;
    private readonly ILogger<ServerCoinService> _logger = logger;
    private readonly SemaphoreSlim _walletLock = new(1, 1);
    private bool disposedValue;

    private async Task<ServerCoinFactory> GetFactory()
    {
        await _walletLock.WaitAsync();
        try
        {
            if (_wallet is null)
            {
                var keyStore = await KeyStore.CreateFrom(_walletProxy);
                _wallet = new StandardWallet(_fullNode, keyStore);
            }
            await _wallet.Sync();
            return new ServerCoinFactory(_fullNode, _wallet);
        }
        finally
        {
            _walletLock.Release();
        }
    }

    public async Task<bool> AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee)
    {
        var factory = await GetFactory();
        return await factory.CreateServerCoin(storeId, new[] { new Uri(serverUrl) }, storeId, mojoReserveAmount, fee);
    }

    public async Task<bool> DeleteServer(string storeId, string coinId, ulong fee)
    {
        var factory = await GetFactory();

        return await factory.DeleteServerCoin(storeId, coinId, fee);
    }

    public async Task<IEnumerable<ServerCoin>> GetCoins(string storeId)
    {
        var factory = await GetFactory();
        return await factory.GetServerCoins(storeId);
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
