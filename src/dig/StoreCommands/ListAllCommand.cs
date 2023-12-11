internal sealed class ListAllCommand()
{
    [Option("t", "timeout", Default = 240, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 240;

    [Option("o", "ours-only", Default = false, Description = "Only list our mirrors.")]
    public bool OursOnly { get; init; }

    [CommandTarget]
    public async Task<int> Execute(StoreManager storeManager)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        await storeManager.ListAll(OursOnly, cancellationSource.Token);
        return 0;
    }
}
