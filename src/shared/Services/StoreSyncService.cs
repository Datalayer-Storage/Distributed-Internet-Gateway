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

    public async Task<int> SyncStores(string mirrorListUri, ulong reserveAmount, bool addMirrors, ulong defaultFee, CancellationToken stoppingToken)
    {
        using var _ = new ScopedLogEntry(_logger, "Syncing subscriptions.");
        try
        {
            _logger.LogInformation("Getting fee");
            var fee = await _chiaService.GetFee(reserveAmount, defaultFee, stoppingToken);

            _logger.LogInformation("Getting subscriptions");
            var subscriptions = await _dataLayer.Subscriptions(stoppingToken);

            _logger.LogInformation("Getting owned stores");
            var ownedStores = await _dataLayer.GetOwnedStores(stoppingToken);

            var mirrorUris = await _mirrorService.GetMyMirrorUris(stoppingToken);
            _logger.LogInformation("Using mirror uri: {mirrorUris}", string.Join(", ", mirrorUris));

            var haveFunds = true;
            var count = 0;
            await foreach (var id in _mirrorService.FetchLatest(mirrorListUri, stoppingToken))
            {
                // don't subscribe or mirror our owned stores
                if (!ownedStores.Contains(id))
                {
                    // subscribing and mirroring are split into two separate operations
                    // as we might subscribe to a singleton that we don't want to mirror
                    // or subscribe to a singleton but not be able to pay for the mirror etc

                    // don't subscribe to a store we already have
                    if (!subscriptions.Contains(id))
                    {
                        _logger.LogInformation("Subscribing to {id}", id);
                        await _dataLayer.Subscribe(id, Enumerable.Empty<string>(), stoppingToken);
                        count++;
                    }

                    // add mirror if we are a mirror server, have a mirror host uri, and have enough funding
                    if (addMirrors && mirrorUris.Any() && haveFunds)
                    {
                        // before mirroring check we have enough funds
                        if (!await AddMirror(id, reserveAmount, fee, mirrorUris, stoppingToken))
                        {
                            _logger.LogWarning("Insufficient funds to add mirror. Pausing mirroring for now.");
                            // if we are out of funds to add mirrors, stop trying but continue subscribing
                            haveFunds = false;
                        }
                    }
                }
            }

            _logger.LogInformation("Done syncing {count} new subscriptions.", count);
            return count;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning("Sync subscriptions timed out: {Message}", ex.GetInnermostExceptionMessage());
            return -1;
        }
        catch (Exception ex)
        {
            _logger.LogError("There was a problem syncing subscriptions: {Message}", ex.GetInnermostExceptionMessage());
        }

        return 0;
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
                catch (ResponseException ex)
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
