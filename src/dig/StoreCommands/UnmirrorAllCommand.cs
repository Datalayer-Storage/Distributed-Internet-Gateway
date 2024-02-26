namespace dig;

internal sealed class UnmirrorAllCommand
{
    [Option("t", "timeout", Default = 240, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 240;

    [Option("f", "fee", ArgumentHelpName = "MOJOS", Description = "Fee override.")]
    public ulong? Fee { get; init; }

    [CommandTarget]
    public async Task<int> Execute(StoreManager storeManager, ChiaService chiaService, IConfiguration configuration)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var fee = await chiaService.ResolveFee(Fee, configuration.GetValue<ulong>("dig:AddMirrorReserveAmount", 0), cts.Token);
        await storeManager.UnmirrorAll(fee, cts.Token);
        return 0;
    }
}
