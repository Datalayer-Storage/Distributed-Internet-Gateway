internal sealed class PeriodicDynDnsService(LoginManager loginManager,
                                            DynDnsService dynDnsService,
                                            ILogger<PeriodicDynDnsService> logger,
                                            IConfiguration configuration) : BackgroundService
{
    private readonly LoginManager _loginManager = loginManager;
    private readonly DynDnsService _dynDnsService = dynDnsService;
    private readonly ILogger<PeriodicDynDnsService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var delay = _configuration.GetValue("dig:DynDnsSyncStartDelaySeconds", 60);
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            // default to once a day
            var period = _configuration.GetValue("dig:DynDnsSyncIntervalMinutes", 1440);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(period));
            do
            {
                var (accessToken, secretKey) = _loginManager.GetCredentials();

                using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var result = await _dynDnsService.UpdateIP(accessToken, secretKey, cancellationSource.Token);
                if (string.IsNullOrEmpty(result))
                {
                    _logger.LogWarning("Unable to update IP address.");
                }
                else
                {
                    _logger.LogInformation("{result}", result.SanitizeForLog());
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
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
