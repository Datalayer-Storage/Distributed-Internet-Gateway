namespace dig.server;

internal sealed class PeriodicNodeSyncService(NodeSyncService syncService,
                                                ChiaService chiaService,
                                                DnsService dnsService,
                                                ILogger<PeriodicNodeSyncService> logger,
                                                IConfiguration configuration) : BackgroundService
{
    private readonly NodeSyncService _syncService = syncService;
    private readonly ChiaService _chiaService = chiaService;
    private readonly DnsService _dnsService = dnsService;
    private readonly ILogger<PeriodicNodeSyncService> _logger = logger;
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
                var mirrorReserveAmount = _configuration.GetValue<ulong>("dig:AddMirrorReserveAmount", 0);
                var serverReserveAmount = _configuration.GetValue<ulong>("dig:ServerCoinReserveAmount", 0);
                var defaultFee = _configuration.GetValue<ulong>("dig:DefaultFee", 500000);
                var fee = await _chiaService.ResolveFee(defaultFee, Math.Max(mirrorReserveAmount, serverReserveAmount), stoppingToken);
                var myDigUri = await _dnsService.GetDigServerUri(stoppingToken) ?? throw new Exception("No dig server uri found");
                var myMirrorUri = await _dnsService.GetMirrorUri(stoppingToken) ?? throw new Exception("No mirror uri found");

                _logger.LogInformation("Using dig server uri: {uri}", myDigUri);
                _logger.LogInformation("Using mirror uri: {uri}", myMirrorUri);

                if (myDigUri.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Server is https. Doubling default reserve amount.");
                    serverReserveAmount *= 2;
                }

                try
                {
                    await _syncService.SyncWithDataLayer(myDigUri,
                                                            myMirrorUri,
                                                            mirrorReserveAmount,
                                                            serverReserveAmount,
                                                            fee,
                                                            stoppingToken);
                }
                catch
                {
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
