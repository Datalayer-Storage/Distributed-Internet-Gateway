internal sealed class SyncPollingService(SyncService syncService,
                                            ILogger<SyncPollingService> logger,
                                            IConfiguration configuration) : BackgroundService
{
    private readonly SyncService _syncService = syncService;
    private readonly ILogger<SyncPollingService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // default to once a day
                var delay = _configuration.GetValue<int>("App:PollingIntervalMinutes", 1440);
                var uri = _configuration.GetValue("App:MirrorServiceUri", "https://api.datalayer.storage/mirrors/v1/list_all") ?? throw new InvalidOperationException("MirrorServiceUri not found");
                var reserveAmount = _configuration.GetValue<ulong>("App:AddMirrorAmount", 300000001);
                var addMirrors = _configuration.GetValue("App:MirrorServer", true);
                var defaultFee =  _configuration.GetValue<ulong>("DlMirrorSync:DefaultFee", 500000);

                await _syncService.SyncSubscriptions(uri, reserveAmount, addMirrors, defaultFee, stoppingToken);

                _logger.LogInformation("Waiting {delay} minutes", delay);
                await Task.Delay(TimeSpan.FromMinutes(delay), stoppingToken);
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
