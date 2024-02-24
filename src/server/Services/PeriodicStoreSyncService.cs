namespace dig.server;

internal sealed class PeriodicStoreSyncService(StoreSyncService syncService,
                                            ILogger<PeriodicStoreSyncService> logger,
                                            IConfiguration configuration) : BackgroundService
{
    private readonly StoreSyncService _syncService = syncService;
    private readonly ILogger<PeriodicStoreSyncService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var delay = _configuration.GetValue("dig:StoreSyncStartDelaySeconds", 30);
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // set/reset the delay period (default to once a day)
                var period = _configuration.GetValue("dig:StoreSyncIntervalMinutes", 1440);
                var mirrorListUri = _configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage/") + "mirrors/v1/list_all";
                var reserveAmount = _configuration.GetValue<ulong>("dig:AddMirrorReserveAmount", 300000001);
                var addMirrors = _configuration.GetValue("dig:MirrorSubscriptions", true);
                var pruneStores = _configuration.GetValue("dig:PruneStores", true);
                var knownOnly = _configuration.GetValue("dig:VerifiedStoresOnly", true);
                var defaultFee = _configuration.GetValue<ulong>("dig:DefaultFee", 500000);

                var (addedCount, removedCount, message) = await _syncService.SyncStores(mirrorListUri, reserveAmount, addMirrors, pruneStores, knownOnly, defaultFee, stoppingToken);
                if (message is not null)
                {
                    // try sooner than regular if the DL is busy
                    period = _configuration.GetValue("dig:DataLayerBusyRetryMinutes", 60);
                    _logger.LogWarning("The data layer appears busy. Will try again later.");
                }

                _logger.LogInformation("Waiting {delay} minutes", period);
                await Task.Delay(TimeSpan.FromMinutes(period), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // When the stopping token is canceled, for example, a call made from services.msc,
            // we shouldn't exit with a non-zero exit code. In other words, this is expected...
        }
        catch (Exception ex)
        {
            if (ex is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.Flatten().InnerExceptions)
                {
                    _logger.LogError(innerException, "One or many tasks failed: {Message}", innerException.Message);
                }
            }
            else
            {
                _logger.LogError(ex, "{Message}", ex.Message);
            }
            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}
