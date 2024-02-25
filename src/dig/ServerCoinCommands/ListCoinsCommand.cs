namespace dig;

using Newtonsoft.Json;

internal sealed class ListCoinsCommand()
{
    [Option("s", "store", ArgumentHelpName = "STORE_ID", Description = "Store ID to delete.")]
    public string Store { get; init; } = string.Empty;

    [Option("o", "owned-only", Description = "Only list our coins.")]
    public bool OwnedOnly { get; init; }

    [CommandTarget]
    public async Task<int> Execute(ServerCoinService serverCoinService)
    {
        if (string.IsNullOrEmpty(Store))
        {
            Console.WriteLine("Store id is required.");
            return -1;
        }

        await Task.CompletedTask;

        try
        {
            var coins = serverCoinService.GetCoins(Store);
            if (coins.Any())
            {
                foreach (dynamic coin in coins)
                {
                    if (OwnedOnly && coin.ours != true)
                    {
                        continue;
                    }
                    Console.WriteLine(JsonConvert.SerializeObject(coin, Formatting.Indented));
                }
            }
            else
            {
                Console.WriteLine("No coins found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("Failed to get coins.");
            return -1;
        }

        return 0;
    }
}
