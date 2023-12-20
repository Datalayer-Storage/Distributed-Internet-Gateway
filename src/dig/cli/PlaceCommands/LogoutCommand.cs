namespace dig.cli;

internal sealed class LogoutCommand()
{
    [CommandTarget]
    public async Task<int> Execute(LoginManager loginManager)
    {
        loginManager.LogOut();
        Console.WriteLine("You have been logged out.");

        await Task.CompletedTask;
        return 0;
    }
}
