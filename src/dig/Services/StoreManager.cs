using chia.dotnet;

internal class StoreManager(DataLayerProxy dataLayer,
                        ILogger<StoreManager> logger)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ILogger<StoreManager> _logger = logger;

    public async Task UnsubscribeAll(bool retain, CancellationToken token = default)
    {
        var subscriptions = await _dataLayer.Subscriptions(token);
        foreach (var subscription in subscriptions)
        {
            _logger.LogInformation("Removing subscription {subscription}", subscription);
            await _dataLayer.Unsubscribe(subscription, retain, token);
        }
    }

    public async Task UnmirrorAll(ulong fee, CancellationToken token = default)
    {
        var subscriptions = await _dataLayer.Subscriptions(token);
        foreach (var subscription in subscriptions)
        {
            var mirrors = await _dataLayer.GetMirrors(subscription, token);

            foreach (var mirror in mirrors.Where(m => m.Ours))
            {
                _logger.LogInformation("Removing mirror for {subscription}", subscription);
                await _dataLayer.DeleteMirror(mirror.CoinId, fee, token);
            }
        }
    }

    public async Task ListAll(bool ours, CancellationToken token = default)
    {
        var subscriptions = await _dataLayer.Subscriptions(token);
        _logger.LogInformation("Found {count} subscriptions.\n", subscriptions.Count());

        foreach (var subscription in subscriptions)
        {
            Console.WriteLine($"Subscription: {subscription}");
            var mirrors = await _dataLayer.GetMirrors(subscription, token);
            mirrors = ours ? mirrors.Where(m => m.Ours) : mirrors;

            foreach (var mirror in mirrors)
            {
                Console.WriteLine($"  Mirror: {mirror.CoinId}");
            }
        }
    }
}
