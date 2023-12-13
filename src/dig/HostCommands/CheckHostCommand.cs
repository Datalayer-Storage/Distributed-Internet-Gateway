namespace dig;

internal sealed class CheckHostCommand()
{
    [Argument(0, Name = "host", Description = "The host to check.", Default = "")]
    public string Host { get; init; } = string.Empty;

    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(HostManager hostManager)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        await hostManager.CheckHost(Host, cancellationSource.Token);
        return 0;
    }
}
