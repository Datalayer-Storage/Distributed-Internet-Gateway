using EasyPipes;
using Microsoft.Extensions.Caching.Memory;

namespace dig.server;

public class ServerService(IHostApplicationLifetime applicationLifetime,
                                    IServiceProvider serviceProvider,
                                    ILogger<ServerService> logger) : IHostedService, IServer
{
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ServerService> _logger = logger;

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

        // start the file cache services

        // If the DIG node is just starting up, we want to clear the cache
        // Because it could be super stale
        var fileCacheService = _serviceProvider.GetRequiredService<FileCacheService>();
        //fileCacheService.Clear();

        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public bool Ping() => true;

    public void Stop() => _applicationLifetime.StopApplication();
}
