using chia.dotnet;

namespace dig;

internal sealed class CheckChiaCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(FullNodeProxy fullNodeProxy, DataLayerProxy dataLayerProxy, WalletProxy walletProxy)
    {
        await HealthZ(dataLayerProxy);
        await HealthZ(fullNodeProxy);
        await HealthZ(walletProxy);

        return 0;
    }

    private async Task HealthZ(ServiceProxy proxy)
    {
        try
        {
            Console.WriteLine($"Checking {proxy.DestinationService} at {proxy.RpcClient.Endpoint.Uri}...");
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
            await proxy.HealthZ(cancellationSource.Token);
            Console.WriteLine($"\tCheck succeeded.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"\tCheck failed: {e.InnerException?.Message ?? e.Message}");
        }
    }
}
