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
    public async Task<int> Execute(ServerCoinService serverCoinService,
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

        using CancellationTokenSource cts = new(10000);
        var url = await dnsService.ResolveHostUrl(Url, cts.Token);
        var fee = await chiaService.ResolveFee(Fee, serverReserve, cts.Token);

        if (serverCoinService.AddServer(Store, url, serverReserve, fee))
        {
            Console.WriteLine("Server coin create transaction submitted.");
        }
        else
        {
            Console.WriteLine("Failed to add server coin.");
            return -1;
        }

        return 0;
    }
}
