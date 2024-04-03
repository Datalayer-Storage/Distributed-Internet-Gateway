using chia.dotnet;

namespace dig;

internal sealed class StoreService(DataLayerProxy dataLayer,
                                    IServerCoinService serverCoinService,
                                    ILogger<StoreService> logger,
                                    IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly IServerCoinService _serverCoinService = serverCoinService;
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
            var serverCoins = await _serverCoinService.GetCoins(storeId);
            foreach (var coin in serverCoins)
            {
                if (coin.Ours && coin.CoinId is not null)
                {
                    string coinId = coin.CoinId;
                    _logger.LogInformation("Removing server coin {coinId} for {storeId}", coinId, storeId);
                    await _serverCoinService.DeleteServer(storeId, coinId, fee);
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

    public async Task<(bool addedStore, bool addedMirror, bool addedServerCoin)> AddStore(string storeId,
                                                                    IEnumerable<string> subscriptions,
                                                                    ulong mirrorReserveAmount,
                                                                    ulong serverReserveAmount,
                                                                    ulong fee,
                                                                    bool addMirrors,
                                                                    string? url,
                                                                    CancellationToken stoppingToken)
    {
        // subscribing, server coin creation and mirroring are split into separate operations
        // as we might subscribe to a singleton that we don't want to mirror
        // or subscribe to a singleton but not be able to pay for the mirror etc

        var addedStore = false;
        var addedMirror = false;
        var addedServerCoin = false;

        // don't subscribe to a store we already have
        if (!subscriptions.Contains(storeId))
        {
            _logger.LogInformation("Subscribing to {id}", storeId);
            await _dataLayer.Subscribe(storeId, [], stoppingToken);
            addedStore = true;
        }

        if (url is not null)
        {
            // add mirror if we are a mirror server, have a mirror host uri, and have enough funding
            if (addMirrors)
            {
                var mirrorUriBuilder = new UriBuilder(url)
                {
                    Port = 8575 //TODO config setting to override
                };
                // before mirroring check we have enough funds
                if (!await AddMirror(storeId, mirrorReserveAmount, fee, mirrorUriBuilder.ToString(), CancellationToken.None))
                {
                    _logger.LogWarning("Insufficient funds to add mirror.");
                    // if we are out of funds to add mirrors, stop trying but continue subscribing
                }
            }

            var serverUriBuilder = new UriBuilder(url)
            {
                Port = _configuration.GetValue("dig:DigServerPort", 41410)
            };

            if (!await AddServer(storeId, serverReserveAmount, fee, serverUriBuilder.ToString(), CancellationToken.None))
            {
                _logger.LogWarning("Insufficient funds to add server coin.");
                // if we are out of funds to add servers, stop trying but continue subscribing
            }
        }
        else if (url is null)
        {
            _logger.LogWarning("No url provided for {id}. Not mirroring or adding server.", storeId);
        }

        return (addedStore, addedMirror, addedServerCoin);
    }

    private async Task<bool> AddServer(string storeId, ulong reserveAmount, ulong fee, string serverUri, CancellationToken stoppingToken)
    {
        var servers = await _serverCoinService.GetCoins(storeId);
        // add any mirrors that aren't already ours
        if (!servers.Any(s => s.Ours))
        {
            _logger.LogInformation("Adding server {id}", storeId);
            var retryCount = 0;
            while (retryCount < 2)
            {
                try
                {
                    // try to add the server - this may fail with insufficient funds
                    await _serverCoinService.AddServer(storeId, serverUri, reserveAmount, fee);
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
