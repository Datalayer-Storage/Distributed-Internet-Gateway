internal sealed class UpdateIPCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(LoginManager loginManager, DynDnsService dynDnsService)
    {
        var (accessToken, secretKey) = loginManager.GetCredentials();

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var result = await dynDnsService.UpdateIP(accessToken, secretKey, cancellationSource.Token);

        Console.WriteLine(result);
        return 0;
    }
}
