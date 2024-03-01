namespace dig.server;

/// <summary>
/// This service is responsible for managing the server's lifecycle.
/// It is called by the server's host to check and stop the server.
/// </summary>
/// <param name="applicationLifetime"></param>
internal class ServerService(IHostApplicationLifetime applicationLifetime) : IServer
{
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

    public bool Ping() => true;

    public void Stop() => _applicationLifetime.StopApplication();
}
