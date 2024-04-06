using chia.dotnet;
using chia.dotnet.wallet;
using dig.servercoin;

namespace Tests;

public class ApiTests
{
    private const ulong RESERVE_AMOUNT = 300042069;

    [SkippableFact]
    public async Task ListCoins()
    {
        Skip.If(Environment.GetEnvironmentVariable("CHIA_ROOT") is null);

        using CancellationTokenSource cts = new(100000);

        var config = Config.Open();
        var fullNodeEndpoint = config.GetEndpoint("full_node");
        var fullNode = new FullNodeProxy(new HttpRpcClient(fullNodeEndpoint), "wallet.tests");

        var walletEndpoint = config.GetEndpoint("wallet");
        var walletProxy = new WalletProxy(new HttpRpcClient(walletEndpoint), "wallet.tests");
        var keyStore = await KeyStore.CreateFrom(walletProxy);

        var wallet = new StandardWallet(fullNode, keyStore);
        await wallet.Sync(new WalletOptions
        {
            MinAddressCount = 50,
            MaxAddressCount = 100,
            UnusedAddressCount = 10,
            InstantCoinRecords = true
        }, cts.Token);

        var serverCoinFactory = new ServerCoinFactory(fullNode, wallet);
        var coins = await serverCoinFactory.GetServerCoins("26632ad912845e6f77714ca0996b21eb647803162dd444275ab5c09bea9426ea", cts.Token);
        Assert.NotEmpty(coins);
    }

    [SkippableFact]
    public async Task DeleteCoin()
    {
        Skip.If(Environment.GetEnvironmentVariable("CHIA_ROOT") is null);

        using CancellationTokenSource cts = new(100000);

        var config = Config.Open();
        var fullNodeEndpoint = config.GetEndpoint("full_node");
        var fullNode = new FullNodeProxy(new HttpRpcClient(fullNodeEndpoint), "wallet.tests");

        var walletEndpoint = config.GetEndpoint("wallet");
        var walletProxy = new WalletProxy(new HttpRpcClient(walletEndpoint), "wallet.tests");
        var keyStore = await KeyStore.CreateFrom(walletProxy);

        var fee = await GetFee(fullNode);

        var wallet = new StandardWallet(fullNode, keyStore);
        await wallet.Sync(null, cts.Token);

        var serverCoinFactory = new ServerCoinFactory(fullNode, wallet);
        var result = await serverCoinFactory.DeleteServerCoin(
            "4551a6bf78f523d1547d6948eda06aca1d275cb1b904c553a7bf32b437fdfcb9",
            config.GetGenesisChallenge() ?? throw new Exception("Genesis challenge not found"),
            fee,
            cts.Token);

        Assert.True(result);
    }

    [SkippableFact]
    public async Task CreateCoin()
    {
        Skip.If(Environment.GetEnvironmentVariable("CHIA_ROOT") is null);

        using CancellationTokenSource cts = new(100000);

        var config = Config.Open();
        var fullNodeEndpoint = config.GetEndpoint("full_node");
        var fullNode = new FullNodeProxy(new HttpRpcClient(fullNodeEndpoint), "wallet.tests");

        var walletEndpoint = config.GetEndpoint("wallet");
        var walletProxy = new WalletProxy(new HttpRpcClient(walletEndpoint), "wallet.tests");
        var keyStore = await KeyStore.CreateFrom(walletProxy);

        var fee = await GetFee(fullNode);

        var wallet = new StandardWallet(fullNode, keyStore);
        await wallet.Sync(null, cts.Token);

        var serverCoinFactory = new ServerCoinFactory(fullNode, wallet);
        var result = await serverCoinFactory.CreateServerCoin(
            "26632ad912845e6f77714ca0996b21eb647803162dd444275ab5c09bea9426ea",
            [new Uri("https://dig.kackman.net:8787")],
            config.GetGenesisChallenge() ?? throw new Exception("Genesis challenge not found"),
            RESERVE_AMOUNT,
            fee,
            cts.Token);

        Assert.True(result is not null);
    }

    private static async Task<ulong> GetFee(FullNodeProxy node)
    {
        using CancellationTokenSource cts = new(10000);
        var fee = await node.GetFeeEstimate(RESERVE_AMOUNT, [5 * 60], cts.Token);

        return fee.estimates.First();
    }
}
