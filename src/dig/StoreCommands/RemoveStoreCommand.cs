using dig.caching;

namespace dig;

internal sealed class RemoveStoreCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID you want to remove.")]
    public string Store { get; init; } = string.Empty;

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [CommandTarget]
    public async Task<int> Execute(StoreService storeService,
                                    ChiaService chiaService,
                                    IObjectCache objectCacheService,
                                    IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(Store))
        {
            Console.WriteLine("Store id is required.");
            return -1;
        }

        ulong mirrorCoinReserve = configuration.GetValue<ulong>("dig:AddMirrorReserveAmount", 0);
        ulong serverCoinReserve = configuration.GetValue<ulong>("dig:ServerCoinReserveAmount", 0);

        using var cts = new CancellationTokenSource(10000);
        var fee = await chiaService.ResolveFee(Fee, Math.Max(serverCoinReserve, mirrorCoinReserve), cts.Token);
        await storeService.RemoveStore(Store, fee, cts.Token);
        objectCacheService.RemoveStore(Store);
        Console.WriteLine($"Removed store.");

        return 0;
    }
}
