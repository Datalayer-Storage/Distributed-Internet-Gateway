internal sealed class LogoutCommand()
{
    [CommandTarget]
    public async Task<int> Execute(LoginManager loginManager)
    {
        loginManager.LogOut();
        await Task.CompletedTask;
        return 0;
    }
}
