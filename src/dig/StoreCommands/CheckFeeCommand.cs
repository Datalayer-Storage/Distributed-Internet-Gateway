namespace dig;

internal sealed class CheckFeeCommand()
{
    [Option("t", "timeout", Default = 240, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 240;

    [Option("c", "cost", Default = 300000001UL, ArgumentHelpName = "MOJOS", Description = "The cost to check the fee against.")]
    public ulong Cost { get; init; } = 300000001UL;

    [CommandTarget]
    public async Task<int> Execute(ChiaService chiaService)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var fee = await chiaService.GetFee(Cost, 0, cancellationSource.Token);
        if (fee > 0)
        {
            Console.WriteLine($"Fee: {fee}");
        }

        return 0;
    }
}
