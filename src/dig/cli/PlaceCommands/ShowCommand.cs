namespace dig.cli;

internal sealed class ShowCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(LoginManager loginManager)
    {
        var encodedAuth = loginManager.GetCredentials();
        if (string.IsNullOrEmpty(encodedAuth))
        {
            Console.WriteLine("Not logged in.");
            return 1;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var myPlace = await loginManager.GetMyPlace(encodedAuth, cancellationSource.Token);
        var dictionary = (IDictionary<string, object>)myPlace;
        foreach (var pair in dictionary.Where(kvp => kvp.Key != "success").OrderBy(kvp => kvp.Key))
        {
            Console.WriteLine($"{pair.Key}: {pair.Value}");
        }
        return 0;
    }
}
