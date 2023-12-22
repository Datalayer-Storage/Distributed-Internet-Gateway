namespace dig;

using System.IO;
using System.IO.Pipes;

internal sealed class StopCommand()
{
    [CommandTarget]
    public async Task<int> Execute(ILogger<StopCommand> logger)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "dig.server.ipc", PipeDirection.Out);
            client.Connect(TimeSpan.FromSeconds(1));
            using var writer = new StreamWriter(client);
            await writer.WriteLineAsync("stop");
            await writer.FlushAsync();
            Console.WriteLine("Server stopped.");
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Server is not running.");
            return -1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop server cleanly.");
            return -1;
        }

        return 0;
    }
}
