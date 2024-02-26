namespace dig;

internal sealed class SyncStoresCommand()
{
    [Argument(0, Name = "uri", Description = "The uri of the remote list.", Default = "https://api.datalayer.storage/mirrors/v1/list_all")]
    public string Uri { get; init; } = string.Empty;

    [Option("s", "subscribe-only", Description = "Only subscribe, do not mirror, each store.")]
    public bool SubscribeOnly { get; init; }

    [Option("p", "prune", Description = "Remove any mirrors/subscriptions that are not in the remote list.")]
    public bool Prune { get; init; }

    [Option("v", "verified-only", Description = "Only subscribe to verified mirrors")]
    public bool VerifiedStoresOnly { get; init; }

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [Option("m", "mirror-reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each mirror coin.")]
    public ulong? MirrorReserve { get; init; } = 300000001UL;

    [Option("r", "server-reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each server coin.")]
    public ulong? ServerReserve { get; init; } = 300000001UL;

    [CommandTarget]
    public async Task<int> Execute(StoreSyncService syncService, ChiaService chiaService, IConfiguration configuration)
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

        // pass CancellationToken.None as we want this to run as long as it takes
        var (addedCount, removedCount, message) = await syncService.SyncStores(Uri,
                                                                                mirrorCoinReserve,
                                                                                serverCoinReserve,
                                                                                !SubscribeOnly,
                                                                                Prune,
                                                                                VerifiedStoresOnly,
                                                                                fee,
                                                                                CancellationToken.None);
        if (message is not null)
        {
            Console.WriteLine("The data layer appears busy. Try again later.\n\t{0}", message);
        }

        Console.WriteLine($"Added {addedCount} and removed {removedCount} stores.");

        return 0;
    }
}
