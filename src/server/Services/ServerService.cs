namespace dig.server;

internal class ServerService(IHostApplicationLifetime applicationLifetime) : IServer
{
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

    public bool Ping() => true;

    public void Stop() => _applicationLifetime.StopApplication();
}
