using chia.dotnet;

namespace dig;

internal sealed class NodeSyncService(DataLayerProxy dataLayer,
                                        IServerCoinService serverCoinService,
                                        ILogger<NodeSyncService> logger)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly IServerCoinService _serverCoinService = serverCoinService;
    private readonly ILogger<NodeSyncService> _logger = logger;

    public async Task<IEnumerable<string>> SyncWithDataLayer(string myDigUri,
                                            string myMirrorUri,
                                            ulong mirrorReserveAmount,
                                            ulong serverReserveAmount,
                                            ulong fee,
                                            CancellationToken stoppingToken)
    {
        _logger.LogInformation("Getting subscriptions");
        var subscriptions = await _dataLayer.Subscriptions(stoppingToken);

        _logger.LogInformation("Getting owned stores");
        var ownedStores = await _dataLayer.GetOwnedStores(stoppingToken);

        // DataLayer is the source of truth for subscriptions, so we need to make sure
        // we have a mirror for each subscription and a server coin for each subscription
        foreach (var subscription in subscriptions)
        {
            // if we have a subscription that isn't owned, make sure it is mirrored
            if (!ownedStores.Any(ownedStore => ownedStore == subscription))
            {
                var mirrors = await _dataLayer.GetMirrors(subscription, stoppingToken);
                if (!mirrors.Any(m => m.Ours))
                {
                    _logger.LogInformation("Adding mirror for {storeId}", subscription);
                    await _dataLayer.AddMirror(subscription, mirrorReserveAmount, [myMirrorUri], fee, stoppingToken);
                }
            }

            _logger.LogInformation("Getting server coins for {storeId}", subscription);
            var serverCoins = await _serverCoinService.GetCoins(subscription, stoppingToken);
            if (!serverCoins.Any(coin => coin.Ours))
            {
                _logger.LogInformation("Adding server coin for {storeId}", subscription);
                if (!await _serverCoinService.AddServer(subscription, myDigUri, serverReserveAmount, fee, stoppingToken))
                {
                    _logger.LogError("Failed to add server coin for {storeId}", subscription);
                }
            }
        }

        return subscriptions;
        //
        // TODO - is there a way to get server coins without a matching subscription?
        // if so, we should delete them
        //
        // if the user unsubscribes from a store outside of dig we will orphan those coins
        // same with mirrors maybe?
        //
    }
}
