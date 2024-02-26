using chia.dotnet;

namespace dig;

internal sealed class StoreSyncService(DataLayerProxy dataLayer,
                                    MirrorService mirrorService,
                                    StoreService storeService,
                                    ILogger<StoreSyncService> logger)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly MirrorService _mirrorService = mirrorService;
    private readonly StoreService _storeService = storeService;
    private readonly ILogger<StoreSyncService> _logger = logger;

    public async Task<(int addedCount, int removedCount, string? message)> SyncStores(string mirrorListUri,
                                                                                        ulong mirrorReserveAmount,
                                                                                        ulong serverReserveAmount,
                                                                                        bool addMirrors,
                                                                                        bool prune,
                                                                                        bool verifiedStoresOnly,
                                                                                        ulong fee,
                                                                                        CancellationToken stoppingToken)
    {
        using var _ = new ScopedLogEntry(_logger, "Syncing subscriptions.");

        var removedCount = 0;
        var addedStoreCount = 0;

        try
        {
            _logger.LogInformation("Getting subscriptions");
            var subscriptions = await _dataLayer.Subscriptions(stoppingToken);

            _logger.LogInformation("Getting owned stores");
            var ownedStores = await _dataLayer.GetOwnedStores(stoppingToken);

            var myMirrorUri = await _mirrorService.GetMyMirrorUri(stoppingToken);
            _logger.LogInformation("Using mirror uri: {myMirrorUri}", myMirrorUri);

            var haveFunds = true;
            var remoteStoreList = await _mirrorService.FetchLatest(mirrorListUri, stoppingToken).ToListAsync(cancellationToken: stoppingToken);
            foreach (var store in remoteStoreList.Where(store => !verifiedStoresOnly || (verifiedStoresOnly && store.is_verified)))
            {
                // don't subscribe or mirror our owned stores
                if (!ownedStores.Contains(store.singleton_id))
                {
                    var addResult = await _storeService.AddStore(store.singleton_id,
                                                                    subscriptions,
                                                                    mirrorReserveAmount,
                                                                    serverReserveAmount,
                                                                    fee,
                                                                    haveFunds,
                                                                    addMirrors,
                                                                    myMirrorUri,
                                                                    stoppingToken);
                    addedStoreCount += addResult.addedStore ? 1 : 0;
                    haveFunds = addResult.haveFunds;
                }
            }
            _logger.LogInformation("Added {count} new subscriptions.", addedStoreCount);

            if (prune)
            {
                foreach (var subscription in subscriptions)
                {
                    // if we have a subscription that isn't in the remote list, remove it
                    if (!remoteStoreList.Any(store => store.singleton_id == subscription))
                    {
                        await _storeService.RemoveStore(subscription, fee, stoppingToken);
                        removedCount++;
                    }
                    // also remove any unverified stores if we are only interested in known stores
                    else if (verifiedStoresOnly && remoteStoreList.Any(store => store.singleton_id == subscription && !store.is_verified))
                    {
                        await _storeService.RemoveStore(subscription, fee, stoppingToken);
                        removedCount++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("There was a problem syncing subscriptions: {Message}", ex.GetInnermostExceptionMessage());
            return (addedStoreCount, removedCount, ex.GetInnermostExceptionMessage());
        }

        return (addedStoreCount, removedCount, null);
    }
}
