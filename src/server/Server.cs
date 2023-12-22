using System.IO.Pipes;

internal class Server(ILogger<Server> logger)
{
    private readonly ILogger<Server> _logger = logger;

    public void Start()
    {
        Task.Run(() => StartPipeServer());
    }

    private async void StartPipeServer()
    {
        try
        {
            using NamedPipeServerStream pipeServer = new("dig.server.ipc", PipeDirection.InOut);
            while (true)
            {
                await pipeServer.WaitForConnectionAsync();

                // Handle the connection in another method
                await HandleConnection(pipeServer);
            }
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to start server.");
            Environment.Exit(-1);
        }
    }

    private async Task HandleConnection(NamedPipeServerStream pipeServer)
    {
        try
        {
            using StreamReader reader = new(pipeServer);
            var line = await reader.ReadLineAsync();

            using StreamWriter writer = new(pipeServer);
            if (line == "stop")
            {
                await writer.WriteLineAsync("stopping");
                await writer.FlushAsync();
                _logger.LogInformation("Stopping server.");
                Environment.Exit(0);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(line);
                await writer.WriteLineAsync("ack");
                await writer.FlushAsync();
            }
        }
        catch (IOException)
        {
            _logger.LogWarning("Pipe broken, client may have disconnected unexpectedly.");
        }
    }
}
