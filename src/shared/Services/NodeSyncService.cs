using chia.dotnet;

namespace dig;

internal sealed class NodeSyncService(DataLayerProxy dataLayer,
                                        ServerCoinService serverCoinService,
                                        DnsService dnsService,
                                        ILogger<NodeSyncService> logger,
                                        IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ServerCoinService _serverCoinService = serverCoinService;
    private readonly DnsService _dnsService = dnsService;
    private readonly ILogger<NodeSyncService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task SyncWithDataLayer(ulong mirrorReserveAmount,
                                            ulong serverReserveAmount,
                                            ulong fee,
                                            CancellationToken stoppingToken)
    {
        _logger.LogInformation("Getting subscriptions");
        var subscriptions = await _dataLayer.Subscriptions(stoppingToken);

        _logger.LogInformation("Getting owned stores");
        var ownedStores = await _dataLayer.GetOwnedStores(stoppingToken);

        var myMirrorUri = await _dnsService.GetMirrorUri(stoppingToken) ?? throw new Exception("No mirror uri found");
        _logger.LogInformation("Using mirror uri: {uri}", myMirrorUri);

        var myDigUri = await _dnsService.GetDigServerUri(stoppingToken) ?? throw new Exception("No dig server uri found");
        _logger.LogInformation("Using dig server uri: {uri}", myDigUri);

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
            var serverCoins = _serverCoinService.GetCoins(subscription);
            if (!serverCoins.Any(coin => coin.ours == true))
            {
                _logger.LogInformation("Adding server coin for {storeId}", subscription);
                if (!_serverCoinService.AddServer(subscription, myDigUri, serverReserveAmount, fee))
                {
                    _logger.LogError("Failed to add server coin for {storeId}", subscription);
                }
            }
        }

        //
        // TODO - is there a way to get server coins without a matching subscription?
        // if so, we should delete them
        //
        // if the user unsubscribes from a store outside of dig we will orphan those coins
        // same with mirrors maybe?
        //
    }
}
