namespace dig;

[Command("server", Description = "Manage the local gateway server.")]
internal sealed class ServerCommands
{
    [Command("check", Description = "Check the status of the server.")]
    public CheckServerCommand Check { get; init; } = new();

    [Command("start", Description = "Start the server in a new process.")]
    public StartCommand Start { get; init; } = new();

    [Command("stop", Description = "Stop the server.")]
    public StopCommand Stop { get; init; } = new();

    [Command("restart", Description = "Stop and start the server.")]
    public ShowCommand Restart { get; init; } = new();
}
