namespace dig;

using System.IO;

internal sealed class DeleteCoinCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID you want to create a server coin for")]
    public string Store { get; init; } = string.Empty;

    [Option("c", "coin", ArgumentHelpName = "COIN_ID", Description = "Coin ID to delete.")]
    public string Coin { get; init; } = string.Empty;

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override")]
    public ulong? Fee { get; init; }

    [CommandTarget]
    public async Task<int> Execute(ServerCoinService serverCoinService,
                                    DnsService dnsService,
                                    ChiaService chiaService,
                                    ILogger<StartCommand> logger,
                                    IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(Store))
        {
            Console.WriteLine("Store id is required.");
            return -1;
        }

        if (string.IsNullOrEmpty(Coin))
        {
            Console.WriteLine("Coin id is required.");
            return -1;
        }

        var fee = await ServerCoinCommands.ResolveFee(Fee, 10000, chiaService, configuration);

        try
        {
            Console.WriteLine(serverCoinService.DeleteServer(Store, Coin, fee));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("Failed to delete server coin.");
            return -1;
        }

        return 0;
    }
}
