using EasyPipes;

namespace dig.server;

public class ServerService(IHostApplicationLifetime applicationLifetime,
                            IServiceProvider serviceProvider) : IHostedService, IServer
{
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // start the IPC channel that the cli uses
        var server = new Server("dig.server.ipc");
        server.RegisterService<IServer>(this);
        server.Start();

        // start the store registry refresh
        var registry = _serviceProvider.GetRequiredService<StoreRegistryService>();
#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
        _ = Task.Run(() => registry.Refresh(cancellationToken), cancellationToken); // run this in the background
#pragma warning restore CA2016 // Forward the 'CancellationToken' parameter to methods

        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public bool Ping() => true;

    public void Stop() => _applicationLifetime.StopApplication();
}
