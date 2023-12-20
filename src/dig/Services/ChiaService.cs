using chia.dotnet;
namespace dig;

internal sealed class ChiaService(FullNodeProxy fullNode,
                                    ILogger<ChiaService> logger,
                                    IConfiguration configuration)
{
    private readonly FullNodeProxy _fullNode = fullNode;
    private readonly ILogger<ChiaService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task<ulong> GetFee(ulong cost, ulong defaultFee, CancellationToken stoppingToken)
    {
        try
        {
            if (!_configuration.GetValue("dig:UseDynamicFee", false))
            {
                return defaultFee;
            }

            using var _ = new ScopedLogEntry(_logger, "Getting fee estimate");
            int[] targetTimes = [_configuration.GetValue("dig:FeeEstimateTargetTimeMinutes", 5) * 60];
            var fee = await _fullNode.GetFeeEstimate(cost, targetTimes, stoppingToken);
            return fee.estimates.First();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not connect to full node. Using default fee amount: {Message}", ex.InnerException?.Message ?? ex.Message);
            return defaultFee;
        }
    }
}
