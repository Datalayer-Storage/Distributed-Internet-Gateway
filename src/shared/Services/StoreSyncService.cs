using chia.dotnet;

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

    public async Task SyncStores(string mirrorListUri, ulong reserveAmount, bool addMirrors, ulong defaultFee, CancellationToken stoppingToken)
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

            var xchWallet = _chiaService.GetWallet(_configuration.GetValue<uint>("dig:XchWalletId", 1));
            var haveFunds = true;
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
                    }

                    // mirror if we are a mirror server, have a mirror host uri, and have enough funding
                    if (addMirrors && mirrorUris.Any() && haveFunds)
                    {
                        // before mirroring check we have enough funds
                        if (await CheckFunds(reserveAmount + fee, xchWallet, stoppingToken))
                        {
                            await AddMirror(id, reserveAmount, fee, mirrorUris, stoppingToken);
                        }
                        else
                        {
                            _logger.LogWarning("Insufficient funds to add mirror. Pausing mirroring for now.");
                            // if we are out of funds to add mirrors, stop trying but continue subscribing
                            haveFunds = false;
                        }
                    }
                }
            }

            _logger.LogInformation("Done syncing");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("There was a problem syncing subscriptions: {Message}", ex.InnerException?.Message ?? ex.Message);
        }
    }

    private async Task<bool> CheckFunds(ulong neededFunds, Wallet xchWallet, CancellationToken stoppingToken)
    {
        var balance = await xchWallet.GetBalance(stoppingToken);
        if (neededFunds < balance.SpendableBalance)
        {
            return true;
        }

        // we don't have enough spendable funding but see if there is change or an incoming confirmed balance
        if (neededFunds < balance.PendingChange || neededFunds < balance.ConfirmedWalletBalance)
        {
            // there's change - wait for it
            var waitingForChangeDelayMinutes = _configuration.GetValue("dig:WaitingForChangeDelayMinutes", 2);
            _logger.LogWarning("Waiting {WaitingForChangeDelayMinutes} minutes for change", waitingForChangeDelayMinutes);
            await Task.Delay(TimeSpan.FromMinutes(waitingForChangeDelayMinutes), stoppingToken);

            // now get the balance again and see if we have enough funds
            balance = await xchWallet.GetBalance(stoppingToken);
        }

        // we've waited return whether we have enough now
        return neededFunds < balance.SpendableBalance;
    }


    private async Task AddMirror(string id, ulong reserveAmount, ulong fee, IEnumerable<string> mirrorUris, CancellationToken stoppingToken)
    {
        var mirrors = await _dataLayer.GetMirrors(id, stoppingToken);
        // add any mirrors that aren't already ours
        if (!mirrors.Any(m => m.Ours))
        {
            _logger.LogInformation("Adding mirror {id}", id);
            await _dataLayer.AddMirror(id, reserveAmount, mirrorUris, fee, stoppingToken);
        }
    }
}
