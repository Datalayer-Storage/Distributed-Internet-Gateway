using chia.dotnet;
using System.Diagnostics;

namespace dig;

internal sealed class AddStoreCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID you want to create a server coin for.")]
    public string Store { get; init; } = string.Empty;

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [Option("m", "mirror-reserve", ArgumentHelpName = "MOJOS", Description = "The amount to reserve with the mirror coin.")]
    public ulong? MirrorReserve { get; init; }

    [Option("p", "precache", Description = "Precache the store's data.")]
    public bool Precache { get; init; }

    [Option("r", "server-reserve", ArgumentHelpName = "MOJOS", Description = "The amount to reserve with the server coin.")]
    public ulong? ServerReserve { get; init; }

    [Option("u", "url", ArgumentHelpName = "URL", Description = "Server url override. If not provided, the server will use the configured url.")]
    public string? Url { get; init; }

    [CommandTarget]
    public async Task<int> Execute(StoreService storeService,
                                    ChiaService chiaService,
                                    DnsService dnsService,
                                    DataLayerProxy dataLayer,
                                    StorePreCacheService storeCacheService,
                                    IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(Store))
        {
            Console.WriteLine("Store id is required.");
            return -1;
        }

        ulong mirrorCoinReserve = MirrorReserve ?? configuration.GetValue<ulong>("dig:AddMirrorReserveAmount", 0);
        if (mirrorCoinReserve == 0)
        {
            Console.WriteLine("Mirror reserve amount is required.");
            return -1;
        }

        ulong serverCoinReserve = ServerReserve ?? configuration.GetValue<ulong>("dig:ServerCoinReserveAmount", 0);
        if (serverCoinReserve == 0)
        {
            Console.WriteLine("Server reserve amount is required.");
            return -1;
        }

        using var cts = new CancellationTokenSource(120000);
        var url = await dnsService.ResolveHostUrl(41410, Url, cts.Token);
        var fee = await chiaService.ResolveFee(Fee, Math.Max(serverCoinReserve, mirrorCoinReserve), cts.Token);
        var subscriptions = await dataLayer.Subscriptions(cts.Token);

        var (addedStore, addedMirror, addedServerCoin) = await storeService.AddStore(Store, subscriptions, mirrorCoinReserve, serverCoinReserve, fee, true, url, cts.Token);
        if (addedStore)
        {
            Console.WriteLine($"Added store.");
        }

        if (addedMirror)
        {
            Console.WriteLine($"Added mirror.");
        }

        if (addedServerCoin)
        {
            Console.WriteLine($"Added server coin.");
        }

        if (Precache)
        {
            Console.WriteLine($"Pre-caching...");
            var stopwatch = Stopwatch.StartNew();
            await storeCacheService.CacheStore(Store, CancellationToken.None);
            stopwatch.Stop();
            Console.WriteLine($"Caching took: {stopwatch.Elapsed.TotalSeconds} seconds");
        }

        return 0;
    }
}
