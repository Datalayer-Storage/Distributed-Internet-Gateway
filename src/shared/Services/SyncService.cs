using chia.dotnet;

internal sealed class SyncService(DataLayerProxy dataLayer,
                                    ChiaService chiaService,
                                    MirrorService mirrorService,
                                    ILogger<SyncService> logger,
                                    IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ChiaService _chiaService = chiaService;
    private readonly MirrorService _mirrorService = mirrorService;
    private readonly ILogger<SyncService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task SyncSubscriptions(string uri, ulong reserveAmount, bool addMirrors, ulong defaultFee, CancellationToken stoppingToken)
    {
        using var _ = new ScopedLogEntry(_logger, "Syncing subscriptions.");
        try
        {
            var fee = await _chiaService.GetFee(reserveAmount, defaultFee, stoppingToken);

            _logger.LogInformation("Getting subscriptions");
            var subscriptions = await _dataLayer.Subscriptions(stoppingToken);

            _logger.LogInformation("Getting owned stores");
            var ownedStores = await _dataLayer.GetOwnedStores(stoppingToken);

            var mirrorUris = await _mirrorService.GetMyMirrorUris(stoppingToken);
            _logger.LogInformation("Using mirror uris: {mirrorUris}", string.Join("\n", mirrorUris));

            var haveFunds = true;

            await foreach (var id in _mirrorService.FetchLatest(uri, stoppingToken))
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

                    // mirror if we are a mirror server, haven't already mirrored and have enough funding
                    if (addMirrors && mirrorUris.Any() && haveFunds)
                    {
                        // if we are out of funds to add mirrors, stop trying but continue subscribing
                        haveFunds = await AddMirror(id, reserveAmount, mirrorUris, fee, stoppingToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("There was a problem syncing subscriptions: {Message}", ex.InnerException?.Message ?? ex.Message);
        }
    }

    private async Task<bool> AddMirror(string id, ulong reserveAmount, IEnumerable<string> mirrorUris, ulong fee, CancellationToken stoppingToken)
    {
        var xchWallet = _chiaService.GetWallet(_configuration.GetValue<uint>("App:XchWalletId", 1));
        var mirrors = await _dataLayer.GetMirrors(id, stoppingToken);
        // add any mirrors that aren't already ours
        if (!mirrors.Any(m => m.Ours))
        {
            var balance = await xchWallet.GetBalance(stoppingToken);
            var neededFunds = reserveAmount + fee;
            if (neededFunds < balance.SpendableBalance)
            {
                _logger.LogInformation("Adding mirror {id}", id);
                await _dataLayer.AddMirror(id, reserveAmount, mirrorUris, fee, stoppingToken);
            }
            else if (balance.SpendableBalance < neededFunds && (neededFunds < balance.PendingChange || neededFunds < balance.ConfirmedWalletBalance))
            {
                // no more spendable funds but we have change incoming, pause and then see if it has arrived
                var waitingForChangeDelayMinutes = _configuration.GetValue("App:WaitingForChangeDelayMinutes", 2);
                _logger.LogWarning("Waiting {WaitingForChangeDelayMinutes} minutes for change", waitingForChangeDelayMinutes);
                await Task.Delay(TimeSpan.FromMinutes(waitingForChangeDelayMinutes), stoppingToken);
            }
            else
            {
                _logger.LogWarning("Insufficient funds to add mirror {id}. Balance={ConfirmedWalletBalance}, Cost={reserveAmount}, Fee={fee}", id, balance.ConfirmedWalletBalance, reserveAmount, fee);
                _logger.LogWarning("Pausing sync for now");
                return false; // out of money, stop mirror syncing
            }
        }

        return true;
    }
}
