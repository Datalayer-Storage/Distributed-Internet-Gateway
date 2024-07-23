using System.Numerics;
using chia.dotnet;

namespace dig;

public sealed class ChiaService(FullNodeProxy fullNode,
                                    WalletProxy wallet,
                                    ILogger<ChiaService> logger,
                                    IConfiguration configuration)
{
    private readonly FullNodeProxy _fullNode = fullNode;
    private readonly WalletProxy _wallet = wallet;
    private readonly ILogger<ChiaService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task<BigInteger> GetNodeWalletBalance(CancellationToken stoppingToken)
    {
        try
        {

            var xchWallet = new Wallet(1, _wallet);
            var balance = await xchWallet.GetBalance(stoppingToken);
            return balance.SpendableBalance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not connect to the wallet {Message}", ex.InnerException?.Message ?? ex.Message);
        }

        return 0;
    }

    public async Task<string> ResolveAddress(string? address, CancellationToken stoppingToken)
    {
        try
        {
            if (string.IsNullOrEmpty(address))
            {
                var xchWallet = new Wallet(1, _wallet);

                return await xchWallet.GetNextAddress(false, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not connect to the wallet {Message}", ex.InnerException?.Message ?? ex.Message);
        }

        return address ?? "";
    }

    public async Task<ulong> ResolveFee(ulong? fee, ulong cost, CancellationToken stoppingToken)
    {
        if (fee.HasValue)
        {
            return fee.Value;
        }

        return await GetFee(cost, _configuration.GetValue<ulong>("dig:DefaultFee", 0), stoppingToken);
    }

    public async Task<ulong> GetFee(ulong cost, ulong defaultFee, CancellationToken stoppingToken)
    {
        try
        {
            if (!_configuration.GetValue("dig:UseDynamicFee", true))
            {
                return defaultFee;
            }

            _logger.LogInformation("Getting fee estimate");
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
