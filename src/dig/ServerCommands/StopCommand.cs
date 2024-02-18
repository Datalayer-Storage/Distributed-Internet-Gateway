using EasyPipes;

namespace dig;

internal sealed class StopCommand()
{
    [CommandTarget]
    public async Task<int> Execute(ILogger<StopCommand> logger)
    {
        await Task.CompletedTask;

        if (ServerProcess.GetIsRunning())
        {
            Console.WriteLine($"Stopping server...");
            ServerProcess.Stop();
            Console.WriteLine($"Server stopped.");
        }
        else
        {
            Console.WriteLine("Server isn't running.");
            return 1;
        }

        return 0;
    }
}
