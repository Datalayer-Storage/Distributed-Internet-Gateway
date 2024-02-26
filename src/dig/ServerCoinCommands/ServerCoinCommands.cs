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
}
