using EasyPipes;

namespace dig;

internal sealed class CheckServerCommand()
{
    [CommandTarget]
    public async Task<int> Execute()
    {
        await Task.CompletedTask;

        Console.WriteLine($"Server is {(ServerProcess.GetIsRunning() ? "up" : "down")}.");

        return 0;
    }
}
