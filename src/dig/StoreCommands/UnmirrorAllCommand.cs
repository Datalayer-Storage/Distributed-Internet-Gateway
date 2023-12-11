internal sealed class UnmirrorAllCommand()
{
    [Option("t", "timeout", Default = 240, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 240;

    [Option("f", "fee", Default = 0UL, ArgumentHelpName = "MOJOS", Description = "Fee to use for each removal transaction.")]
    public ulong Fee { get; init; } = 0UL;

    [CommandTarget]
    public async Task<int> Execute(StoreManager storeManager)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        await storeManager.UnmirrorAll(Fee, cancellationSource.Token);
        return 0;
    }
}
