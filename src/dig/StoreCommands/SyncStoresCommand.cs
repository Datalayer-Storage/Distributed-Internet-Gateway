namespace dig;

internal sealed class SyncStoresCommand()
{
    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [Option("m", "mirror-reserve", ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each mirror coin.")]
    public ulong? MirrorReserve { get; init; }

    [Option("p", "precache", Description = "Precache the store's data.")]
    public bool Precache { get; init; }

    [Option("r", "server-reserve", ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each server coin.")]
    public ulong? ServerReserve { get; init; }

    [CommandTarget]
    public async Task<int> Execute(NodeSyncService syncService,
                                    ChiaService chiaService,
                                    DnsService dnsService,
                                    StorePreCacheService storeCacheService,
                                    IConfiguration configuration)
    {
        ulong mirrorCoinReserve = MirrorReserve ?? configuration.GetValue<ulong>("dig:AddMirrorReserveAmount", 0);
        if (mirrorCoinReserve == 0)
        {
            Console.WriteLine("Mirror reserve amount is required.");
            return -1;
        }

        ulong serverReserve = ServerReserve ?? configuration.GetValue<ulong>("dig:ServerCoinReserveAmount", 0);
        if (serverReserve == 0)
        {
            Console.WriteLine("Server reserve amount is required.");
            return -1;
        }

        using var cts = new CancellationTokenSource(60000);
        var fee = await chiaService.ResolveFee(Fee, mirrorCoinReserve, cts.Token);

        var myDigUri = await dnsService.GetDigServerUri(cts.Token) ?? throw new Exception("No dig server uri found");
        var myMirrorUri = await dnsService.GetMirrorUri(cts.Token) ?? throw new Exception("No mirror uri found");

        Console.WriteLine($"Using dig server uri: {myDigUri}");
        Console.WriteLine($"Using mirror uri: {myMirrorUri}");

        // if the user didn't supply a reserve amount AND the server is https, double the default reserve amount
        if (ServerReserve is null && myDigUri.StartsWith("https", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Server is https. Doubling default reserve amount.");
            serverReserve *= 2;
        }

        // pass CancellationToken.None as we want this to run as long as it takes (DL can be slow when busy)
        var stores = await syncService.SyncWithDataLayer(myDigUri,
                                                            myMirrorUri,
                                                            mirrorCoinReserve,
                                                            serverReserve,
                                                            fee,
                                                            CancellationToken.None);

        if (Precache)
        {
            Console.WriteLine($"Caching...");

            foreach (var store in stores)
            {
                await storeCacheService.CacheStore(store, CancellationToken.None);
            }
        }

        Console.WriteLine("Sync complete.");

        return 0;
    }
}
