internal sealed class SyncStoresCommand()
{
    [Argument(0, Name = "uri", Description = "The uri of the remote list.", Default = "https://api.datalayer.storage/mirrors/v1/list_all")]
    public string Uri { get; init; } = string.Empty;

    [Option("s", "subscribe-only", Default = false, Description = "Only subscribe, do not mirror, each store")]
    public bool SubscribeOnly { get; init; } = false;

    [Option("f", "fee", Default = 0UL, ArgumentHelpName = "MOJOS", Description = "Default fee to use for each mirror transaction.")]
    public ulong Fee { get; init; } = 0UL;

    [Option("r", "reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each mirror coin.")]
    public ulong Reserve { get; init; } = 300000001UL;

    [Option("t", "timeout", Default = 240, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 240;

    [CommandTarget]
    public async Task<int> Execute(StoreSyncService syncService)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        await syncService.SyncStores(Uri, Reserve, !SubscribeOnly, Fee, cancellationSource.Token);
        return 0;
    }
}
