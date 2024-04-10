namespace dig;

internal sealed class AddCoinCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID you want to create a server coin for")]
    public string Store { get; init; } = string.Empty;

    [Option("u", "url", ArgumentHelpName = "URL", Description = "Server url override. If not provided, the server will use the configured url")]
    public string? Url { get; init; }

    [Option("r", "server-reserve", ArgumentHelpName = "MOJOS", Description = "Server reserve amount override. If not provided, the server will use the configured reserve amount")]
    public ulong? ServerReserve { get; init; }

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [CommandTarget]
    public async Task<int> Execute(IServerCoinService serverCoinService,
                                    DnsService dnsService,
                                    ChiaService chiaService,
                                    IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(Store))
        {
            Console.WriteLine("Store id is required.");
            return -1;
        }

        ulong serverReserve = ServerReserve ?? configuration.GetValue<ulong>("dig:ServerCoinReserveAmount", 0);
        if (serverReserve == 0)
        {
            Console.WriteLine("Reserve amount is required.");
            return -1;
        }

        using CancellationTokenSource cts = new(100000);
        var url = await dnsService.ResolveHostUrl(41410, Url, cts.Token);
        var fee = await chiaService.ResolveFee(Fee, serverReserve, cts.Token);

        // if the user didn't supply a reserve amount AND the server is https, double the default reserve amount
        if (ServerReserve is null && url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Server is https. Doubling default reserve amount.");
            serverReserve *= 2;
        }

        var coin = await serverCoinService.CreateCoin(Store, url, serverReserve, fee, cts.Token);
        if (coin is not null)
        {
            Console.WriteLine($"Server coin with an id of {coin.CoinId} transaction submitted.");
        }
        else
        {
            Console.WriteLine("Failed to add server coin.");
            return -1;
        }

        return 0;
    }
}
