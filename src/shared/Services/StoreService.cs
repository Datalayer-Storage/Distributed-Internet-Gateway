using chia.dotnet;

namespace dig;

internal sealed class StoreService(DataLayerProxy dataLayer,
                                    ServerCoinService serverCoinService,
                                    ILogger<StoreService> logger,
                                    IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly ServerCoinService _serverCoinService = serverCoinService;
    private readonly ILogger<StoreService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task RemoveStore(string storeId, ulong fee, CancellationToken stoppingToken)
    {
        var mirrors = await _dataLayer.GetMirrors(storeId, stoppingToken);

        foreach (var mirror in mirrors.Where(m => m.Ours))
        {
            _logger.LogInformation("Removing mirror for {subscription}", storeId);
            await _dataLayer.DeleteMirror(mirror.CoinId, fee, stoppingToken);
        }

        try
        {
            var serverCoins = _serverCoinService.GetCoins(storeId);
            foreach (var coin in serverCoins)
            {
                if (coin.ours == true && coin.coin_id is not null)
                {
                    string coinId = coin.coin_id.ToString();
                    _logger.LogInformation("Removing server coin {coinId} for {storeId}", coinId, storeId);
                    _serverCoinService.DeleteServer(storeId, coinId, fee);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("There was an error removing server coins for {id}: {message}", storeId, ex.Message);
        }

        _logger.LogInformation("Unsubscribing from {subscription}", storeId);
        await _dataLayer.Unsubscribe(storeId, false, stoppingToken);
    }

    public async Task<(bool haveFunds, bool addedStore)> AddStore(string storeId,
                                                                    IEnumerable<string> subscriptions,
                                                                    ulong mirrorReserveAmount,
                                                                    ulong serverReserveAmount,
                                                                    ulong fee,
                                                                    bool haveFunds,
                                                                    bool addMirrors,
                                                                    string? url,
                                                                    CancellationToken stoppingToken)
    {
        // subscribing, server coin creation and mirroring are split into separate operations
        // as we might subscribe to a singleton that we don't want to mirror
        // or subscribe to a singleton but not be able to pay for the mirror etc

        // don't subscribe to a store we already have
        var addedStore = false;
        if (!subscriptions.Contains(storeId))
        {
            _logger.LogInformation("Subscribing to {id}", storeId);
            await _dataLayer.Subscribe(storeId, [], stoppingToken);
            addedStore = true;
        }

        if (url is not null && haveFunds)
        {
            // add mirror if we are a mirror server, have a mirror host uri, and have enough funding
            if (addMirrors)
            {
                // before mirroring check we have enough funds
                if (!await AddMirror(storeId, mirrorReserveAmount, fee, url, CancellationToken.None))
                {
                    _logger.LogWarning("Insufficient funds to add mirror.");
                    // if we are out of funds to add mirrors, stop trying but continue subscribing
                    haveFunds = false;
                }
            }

            if (!await AddServer(storeId, mirrorReserveAmount, fee, url, CancellationToken.None))
            {
                _logger.LogWarning("Insufficient funds to add server.");
                // if we are out of funds to add servers, stop trying but continue subscribing
                haveFunds = false;
            }
        }
        else if (url is null)
        {
            _logger.LogWarning("No url provided for {id}. Not mirroring or adding server.", storeId);
        }
        else if (!haveFunds)
        {
            _logger.LogWarning("Insufficient funds to add mirror and server for {id}.", storeId);
        }

        return (haveFunds, addedStore);
    }

    private async Task<bool> AddServer(string storeId, ulong reserveAmount, ulong fee, string serverUri, CancellationToken stoppingToken)
    {
        var servers = _serverCoinService.GetCoins(storeId);
        // add any mirrors that aren't already ours
        if (!servers.Any(s => s.ours == true))
        {
            _logger.LogInformation("Adding server {id}", storeId);
            var retryCount = 0;
            while (retryCount < 2)
            {
                try
                {
                    // try to add the server - this may fail with insufficient funds
                    _serverCoinService.AddServer(storeId, serverUri, reserveAmount, fee);
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

    private async Task<bool> AddMirror(string storeId, ulong reserveAmount, ulong fee, string mirrorUri, CancellationToken stoppingToken)
    {
        var mirrors = await _dataLayer.GetMirrors(storeId, stoppingToken);
        // add any mirrors that aren't already ours
        if (!mirrors.Any(m => m.Ours))
        {
            _logger.LogInformation("Adding mirror {id}", storeId);
            var retryCount = 0;
            while (retryCount < 2)
            {
                try
                {
                    // try to add the mirror - this may fail with insufficient funds
                    await _dataLayer.AddMirror(storeId, reserveAmount, [mirrorUri], fee, stoppingToken);
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
