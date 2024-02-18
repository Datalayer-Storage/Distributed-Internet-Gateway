using chia.dotnet;

namespace dig;

internal sealed class StoreSyncService(DataLayerProxy dataLayer,
                                    ChiaService chiaService,
                                    MirrorService mirrorService,
                                    ILogger<StoreSyncService> logger,
                                    IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ChiaService _chiaService = chiaService;
    private readonly MirrorService _mirrorService = mirrorService;
    private readonly ILogger<StoreSyncService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task<(int addedCount, int removedCount, string? message)> SyncStores(string mirrorListUri,
                                                                                        ulong reserveAmount,
                                                                                        bool addMirrors,
                                                                                        bool prune,
                                                                                        bool knownOnly,
                                                                                        ulong defaultFee,
                                                                                        CancellationToken stoppingToken)
    {
        using var _ = new ScopedLogEntry(_logger, "Syncing subscriptions.");

        var removedCount = 0;
        var addedStoreCount = 0;

        try
        {
            _logger.LogInformation("Getting fee");
            var fee = await _chiaService.GetFee(reserveAmount, defaultFee, stoppingToken);

            _logger.LogInformation("Getting subscriptions");
            var subscriptions = await _dataLayer.Subscriptions(stoppingToken);

            _logger.LogInformation("Getting owned stores");
            var ownedStores = await _dataLayer.GetOwnedStores(stoppingToken);

            var myMirrorUris = await _mirrorService.GetMyMirrorUris(stoppingToken);
            _logger.LogInformation("Using mirror uri: {mirrorUris}", string.Join(", ", myMirrorUris));

            var haveFunds = true;
            var remoteStoreList = await _mirrorService.FetchLatest(mirrorListUri, stoppingToken).ToListAsync(cancellationToken: stoppingToken);
            foreach (var store in remoteStoreList.Where(store => !knownOnly || (knownOnly && store.is_verified)))
            {
                // don't subscribe or mirror our owned stores
                if (!ownedStores.Contains(store.singleton_id))
                {
                    var addResult = await AddStore(store, subscriptions, reserveAmount, fee, haveFunds, addMirrors, myMirrorUris, stoppingToken);
                    addedStoreCount += addResult.addedCount;
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
                        await RemoveStore(fee, subscription, stoppingToken);
                        removedCount++;
                    }
                    // also remove any unverified stores if we are only interested in known stores
                    else if (knownOnly && remoteStoreList.Any(store => store.singleton_id == subscription && !store.is_verified))
                    {
                        await RemoveStore(fee, subscription, stoppingToken);
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

    private async Task RemoveStore(ulong fee, string store, CancellationToken stoppingToken)
    {
        var mirrors = await _dataLayer.GetMirrors(store, stoppingToken);

        foreach (var mirror in mirrors.Where(m => m.Ours))
        {
            _logger.LogInformation("Removing mirror for {subscription}", store);
            await _dataLayer.DeleteMirror(mirror.CoinId, fee, stoppingToken);
        }

        _logger.LogInformation("Unsubscribing from {subscription}", store);
        await _dataLayer.Unsubscribe(store, false, stoppingToken);
    }

    private async Task<(bool haveFunds, int addedCount)> AddStore(Store store, IEnumerable<string> subscriptions, ulong reserveAmount, ulong fee, bool haveFunds, bool addMirrors, IEnumerable<string> myMirrorUris, CancellationToken stoppingToken)
    {
        // subscribing and mirroring are split into two separate operations
        // as we might subscribe to a singleton that we don't want to mirror
        // or subscribe to a singleton but not be able to pay for the mirror etc

        // don't subscribe to a store we already have
        var addedCount = 0;
        if (!subscriptions.Contains(store.singleton_id))
        {
            _logger.LogInformation("Subscribing to {id}", store);
            await _dataLayer.Subscribe(store.singleton_id, Enumerable.Empty<string>(), stoppingToken);
            addedCount = 1;
        }

        // add mirror if we are a mirror server, have a mirror host uri, and have enough funding
        if (addMirrors && myMirrorUris.Any() && haveFunds)
        {
            // before mirroring check we have enough funds
            if (!await AddMirror(store.singleton_id, reserveAmount, fee, myMirrorUris, stoppingToken))
            {
                _logger.LogWarning("Insufficient funds to add mirror. Pausing mirroring for now.");
                // if we are out of funds to add mirrors, stop trying but continue subscribing
                haveFunds = false;
            }
        }

        return (haveFunds, addedCount);
    }

    private async Task<bool> AddMirror(string id, ulong reserveAmount, ulong fee, IEnumerable<string> mirrorUris, CancellationToken stoppingToken)
    {
        var mirrors = await _dataLayer.GetMirrors(id, stoppingToken);
        // add any mirrors that aren't already ours
        if (!mirrors.Any(m => m.Ours))
        {
            _logger.LogInformation("Adding mirror {id}", id);
            var retryCount = 0;
            while (retryCount < 2)
            {
                try
                {
                    // try to add the mirror - this may fail with insufficient funds
                    await _dataLayer.AddMirror(id, reserveAmount, mirrorUris, fee, stoppingToken);
                    // if we succeeded, return true
                    return true;
                }
                catch (ResponseException)
                {
                    // if we get an exception, try to add the mirror again
                    var waitingForChangeDelayMinutes = _configuration.GetValue("dig:WaitingForChangeDelayMinutes", 2);
                    _logger.LogWarning("Waiting {WaitingForChangeDelayMinutes} minutes for change", waitingForChangeDelayMinutes);
                    await Task.Delay(TimeSpan.FromMinutes(waitingForChangeDelayMinutes), stoppingToken);
                    retryCount++;
                }
            }

            // we've tried twice and failed
            return false;
        }

        // we already have a mirror
        return true;
    }
}
