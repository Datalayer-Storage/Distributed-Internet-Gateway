using chia.dotnet;
namespace dig;


internal sealed class CheckChiaCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(FullNodeProxy fullNodeProxy, DataLayerProxy dataLayerProxy, WalletProxy walletProxy)
    {
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));

        await HealthZ(fullNodeProxy, cancellationSource.Token);
        await HealthZ(dataLayerProxy, cancellationSource.Token);
        await HealthZ(walletProxy, cancellationSource.Token);

        return 0;
    }

    private static async Task HealthZ(ServiceProxy proxy, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"Checking {proxy.DestinationService} at {proxy.RpcClient.Endpoint.Uri}...");
            await proxy.HealthZ(cancellationToken);
            Console.WriteLine($"\tCheck succeeded.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"\tCheck failed: {e.InnerException?.Message ?? e.Message}");
        }
    }
}
