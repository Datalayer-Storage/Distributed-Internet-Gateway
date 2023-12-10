internal sealed class UnsubscribeAllCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

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
