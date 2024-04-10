namespace dig;

using System.IO;

internal sealed class DeleteCoinCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID you want to create a server coin for")]
    public string Store { get; init; } = string.Empty;

    [Option("c", "coin", ArgumentHelpName = "COIN_ID", Description = "Coin ID to delete.")]
    public string Coin { get; init; } = string.Empty;

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [CommandTarget]
    public async Task<int> Execute(IServerCoinService serverCoinService, ChiaService chiaService, IConfiguration configuration)
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

        using CancellationTokenSource cts = new(100000);
        var fee = await chiaService.ResolveFee(Fee, configuration.GetValue<ulong>("dig:ServerCoinReserveAmount", 300000), cts.Token);

        Console.WriteLine(await serverCoinService.SpendCoin(Store, Coin, fee, cts.Token));

        return 0;
    }
}
