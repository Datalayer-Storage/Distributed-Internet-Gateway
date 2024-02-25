namespace dig;

[Command("coins", Description = "Manage server coins.")]
internal sealed class ServerCoinCommands
{
    [Command("add", Description = "Add a server coin.")]
    public AddCoinCommand Check { get; init; } = new();

    [Command("delete", Description = "Delete a server coin.")]
    public DeleteCoinCommand Start { get; init; } = new();

    [Command("list", Description = "List the servers associated with a coin.")]
    public ListCoinsCommand Stop { get; init; } = new();

    public static async Task<ulong> ResolveFee(ulong? fee, ulong cost, ChiaService chiaService, IConfiguration configuration)
    {
        if (fee.HasValue)
        {
            return fee.Value;
        }

        try
        {
            using var cts = new CancellationTokenSource(10000);
            return await chiaService.GetFee(cost, configuration.GetValue<ulong>("dig:DefaultFee", 0), cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("Failed to get fee. Defaulting to 0.");
        }

        return 0;
    }
}
