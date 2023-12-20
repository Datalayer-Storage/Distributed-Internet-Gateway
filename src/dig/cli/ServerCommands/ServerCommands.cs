namespace dig.cli;

[Command("server", Description = "Manage the gateway server.")]
internal sealed class ServerCommands
{
    [Command("run", Description = "Run the server process.")]
    public RunCommand Login { get; init; } = new();

    [Command("start", Description = "Start the server in a new process.")]
    public LogoutCommand Logout { get; init; } = new();

    [Command("stop", Description = "Stop the server.")]
    public ShowCommand Show { get; init; } = new();

    [Command("restart", Description = "Stop the server.")]
    public ShowCommand Restart { get; init; } = new();
}
