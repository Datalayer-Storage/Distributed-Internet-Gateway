internal sealed class LoginCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(LoginManager loginManager)
    {
        var credentials = loginManager.GetCredentials();
        if (!string.IsNullOrEmpty(credentials))
        {
            Console.WriteLine("You are already logged in. Logout and try again.");
            return 1;
        }

        Console.WriteLine("In order to access the DataLayer API you must login.");
        Console.WriteLine("If you do not already have an access token and secret key visit https://datalayer.storage to create an account.\n");

        Console.Write("Access token: ");
        var accessToken = Console.ReadLine();
        if (string.IsNullOrEmpty(accessToken))
        {
            Console.WriteLine("Access token is required.");
            return 1;
        }

        Console.Write("Secret key: ");
        var secretKey = ReadSecret();
        if (string.IsNullOrEmpty(secretKey))
        {
            Console.WriteLine("Secret key is required.");
            return 1;
        }

        Console.WriteLine();

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var proxyKey = await loginManager.Login(accessToken, secretKey, cancellationSource.Token);
        if (string.IsNullOrEmpty(proxyKey))
        {
            Console.WriteLine("Login failed.");
            return 1;
        }

        Console.WriteLine($"Proxy Key: {proxyKey}");

        return 0;
    }

    private static string ReadSecret()
    {
        var secret = "";
        while (true)
        {
            var key = Console.ReadKey(true);
            // Break the loop when Enter key is pressed
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                return string.Empty;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                {
                    secret = secret.Remove(secret.Length - 1);
                    Console.Write("\b \b");
                }
                continue;
            }
            secret += key.KeyChar;
            Console.Write("");
        }

        return secret;
    }
}
