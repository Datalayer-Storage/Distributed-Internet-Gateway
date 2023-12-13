namespace dig;

internal sealed class UnsubscribeAllCommand()
{
    [Option("t", "timeout", Default = 240, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 240;

    [Option("r", "retain", Default = false, Description = "Retain files when unsubscribing.")]
    public bool Retain { get; init; } = false;

    [CommandTarget]
    public async Task<int> Execute(StoreManager storeManager)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        await storeManager.UnsubscribeAll(Retain, cancellationSource.Token);
        return 0;
    }
}
