namespace dig;

internal sealed class AddCoinCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID you want to create a server coin for")]
    public string Store { get; init; } = string.Empty;

    [Option("u", "url", ArgumentHelpName = "URL", Description = "Server url override. If not provided, the server will use the configured url")]
    public string? Url { get; init; }

    [Option("r", "reserve", ArgumentHelpName = "MOJOS", Description = "Coin reserve amount override. If not provided, the server will use the configured reserve amount")]
    public ulong? Reserve { get; init; }

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override")]
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

        ulong reserve = Reserve ?? configuration.GetValue<ulong>("dig:ServerCoinDefaultReserveAmountMojos", 0);
        if (reserve == 0)
        {
            Console.WriteLine("Reserve amount is required.");
            return -1;
        }

        string url;
        try
        {
            url = await GetHostUrl(dnsService);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("Failed to get public ip.");
            return -1;
        }

        var fee = await ServerCoinCommands.ResolveFee(Fee, 10000, chiaService, configuration);

        try
        {
            if (serverCoinService.AddServer(Store, url, reserve, fee))
            {
                Console.WriteLine("Server coin create transaction submitted.");
            }
            else
            {
                Console.WriteLine("Failed to add server coin.");
                return -1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("Failed to add server coin.");
            return -1;
        }

        return 0;
    }

    private async Task<string> GetHostUrl(DnsService dnsService)
    {
        if (!string.IsNullOrEmpty(Url))
        {
            return Url.ToString();
        }

        using var cts = new CancellationTokenSource(10000);
        return await dnsService.GetHostUri(cts.Token) ?? throw new Exception("Failed to get public ip.");
    }
}
