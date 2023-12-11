internal sealed class CheckFeeCommand()
{
    [Option("t", "timeout", Default = 240, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 240;

    [Option("r", "reserve", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The amount to reserve with each mirror coin.")]
    public ulong Reserve { get; init; } = 300000001UL;

    [CommandTarget]
    public async Task<int> Execute(ChiaService chiaService)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var fee = await chiaService.GetFee(Reserve, 0, cancellationSource.Token);
        if (fee > 0)
        {
            Console.WriteLine($"Fee: {fee}");
        }
        return 0;
    }
}
