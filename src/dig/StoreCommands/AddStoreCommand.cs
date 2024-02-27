using chia.dotnet;

namespace dig;

internal sealed class AddStoreCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID you want to create a server coin for.")]
    public string Store { get; init; } = string.Empty;

    [Option("u", "url", ArgumentHelpName = "URL", Description = "Server url override. If not provided, the server will use the configured url.")]
    public string? Url { get; init; }

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [Option("m", "mirror-reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with the mirror coin.")]
    public ulong? MirrorReserve { get; init; } = 300000001UL;

    [Option("r", "server-reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with the server coin.")]
    public ulong? ServerReserve { get; init; } = 300000001UL;

    [CommandTarget]
    public async Task<int> Execute(StoreService storeService,
                                    ChiaService chiaService,
                                    DnsService dnsService,
                                    DataLayerProxy dataLayer,
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

        using var cts = new CancellationTokenSource(10000);
        var url = await dnsService.ResolveHostUrl(41410, Url, cts.Token);
        var fee = await chiaService.ResolveFee(Fee, Math.Max(serverCoinReserve, mirrorCoinReserve), cts.Token);
        var subscriptions = await dataLayer.Subscriptions(cts.Token);

        var (haveFunds, addedStore) = await storeService.AddStore(Store, subscriptions, mirrorCoinReserve, serverCoinReserve, fee, true, true, url, cts.Token);
        if (addedStore)
        {
            Console.WriteLine($"Added store.");
        }
        else
        {
            Console.WriteLine("Did not add store. It may already exist."); // TODO improve this
        }

        return 0;
    }
}
