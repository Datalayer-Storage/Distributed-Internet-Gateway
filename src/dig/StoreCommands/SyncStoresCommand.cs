namespace dig;

internal sealed class SyncStoresCommand()
{
    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [Option("m", "mirror-reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each mirror coin.")]
    public ulong? MirrorReserve { get; init; } = 300000001UL;

    [Option("p", "precache", Description = "Precache the store's data.")]
    public bool Precache { get; init; }

    [Option("r", "server-reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each server coin.")]
    public ulong? ServerReserve { get; init; } = 300000001UL;

    [CommandTarget]
    public async Task<int> Execute(NodeSyncService syncService,
                                    ChiaService chiaService,
                                    StoreUpdateNotifierService storeUpdateNotifierService,
                                    IConfiguration configuration)
    {
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

        using var cts = new CancellationTokenSource(10000);
        var fee = await chiaService.ResolveFee(Fee, mirrorCoinReserve, cts.Token);

        // pass CancellationToken.None as we want this to run as long as it takes (DL can be slow when busy)
        var stores = await syncService.SyncWithDataLayer(mirrorCoinReserve,
                                        serverCoinReserve,
                                        fee,
                                        CancellationToken.None);


        if (Precache)
        {
            Console.WriteLine($"Caching...");

            foreach (var store in stores)
            {
                await storeUpdateNotifierService.PreCacheStore(store, CancellationToken.None);
            }
        }

        Console.WriteLine("Sync complete.");

        return 0;
    }
}
