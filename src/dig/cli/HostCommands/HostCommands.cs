namespace dig.cli;

[Command("host", Description = "Manage the host.")]
internal sealed class HostCommands
{
    [Command("check", Description = "Verify that a mirror host is accessible.")]
    public CheckHostCommand Check { get; init; } = new();

    [Command("check-chia", Description = "Check accessibility to chia endpoints.")]
    public CheckChiaCommand CheckChia { get; init; } = new();

    [Command("show-config", Description = "Shows the current host configuration.")]
    public ShowConfigCommand ShowConfig { get; init; } = new();
}
