namespace dig.cli;

internal sealed class UpdateIPCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(LoginManager loginManager, DynDnsService dynDnsService)
    {
        var encodedAuth = loginManager.GetCredentials();
        if (string.IsNullOrEmpty(encodedAuth))
        {
            Console.WriteLine("Not logged in.");
            return 1;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var result = await dynDnsService.UpdateIP(encodedAuth, cancellationSource.Token);

        Console.WriteLine(result);
        return 0;
    }
}
