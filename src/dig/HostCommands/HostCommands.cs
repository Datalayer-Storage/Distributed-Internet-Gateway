[Command("host", Description = "Manage the host.")]
internal sealed class HostCommands
{
    [Command("check", Description = "Verify that a mirror host is accessible.")]
    public CheckHostCommand CheckHost { get; init; } = new();
}
