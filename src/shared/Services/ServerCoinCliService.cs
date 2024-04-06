namespace dig;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using dig.servercoin;

public class ServerCoinCliService(ChiaConfig chiaConfig,
                                        ILogger<ServerCoinCliService> logger,
                                        IConfiguration configuration) : IServerCoinService
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly ILogger<ServerCoinCliService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task <bool> AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee, CancellationToken token = default)
    {
        List<string> args = ["add_server", "--storeId", storeId, "--url", serverUrl, "--amount", mojoReserveAmount.ToString(), "--fee", fee.ToString()];

        var result = RunCommand(args).Trim();
        await Task.CompletedTask;
        return result == "true";
    }

    public async Task<bool> DeleteServer(string storeId, string coinId, ulong fee, CancellationToken token = default)
    {
        List<string> args = ["delete_server", "--storeId", storeId, "--coinId", coinId, "--fee", fee.ToString()];

        await Task.CompletedTask;

        RunCommand(args);

        return true;
    }

    public async Task<IEnumerable<ServerCoin>> GetCoins(string storeId, CancellationToken token = default)
    {
        List<string> args = ["get_server_coins", "--storeId", storeId];

        var json = RunCommand(args);
        if (!string.IsNullOrEmpty(json))
        {
            var responseShape = new { Servers = new List<ServerCoin>() };
            var serializationSettings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
            };
            var response = JsonConvert.DeserializeAnonymousType(json, responseShape, serializationSettings)
                ?? throw new Exception("Failed to parse server coin result");

            return response.Servers;
        }

        await Task.CompletedTask;

        return [];
    }

    private string RunCommand(IList<string> args)
    {
        var programPath = _configuration.GetValue("dig:ServerCoinExePath", "");

        if (string.IsNullOrEmpty(programPath))
        {
            var programName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "server_coin.exe" : "server_coin";
            programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, programName);
        }

        if (!File.Exists(programPath))
        {
            throw new Exception($"Could not locate the server coin executable at {programPath}");
        }

        // here we pick up the full node and wallet endpoints from the already configured full node and wallet
        var fullNode = _chiaConfig.GetEndpoint("full_node") ?? throw new Exception("Full node endpoint not found");
        var wallet = _chiaConfig.GetEndpoint("wallet") ?? throw new Exception("Wallet endpoint not found");

        if (fullNode.Uri.Host != wallet.Uri.Host)
        {
            throw new Exception("The full node and wallet must be on the same host for server coin to work.");
        }

        // server_coin error if passed non-default ports

        args.Add("--fullNodeHost");
        args.Add(fullNode.Uri.Host);
        // args.Add("--fullNodePort");
        // args.Add(fullNode.Uri.Port.ToString());

        args.Add("--walletHost");
        args.Add(wallet.Uri.Host);
        // args.Add("--walletPort");
        // args.Add(wallet.Uri.Port.ToString());

        var certificateFolderPath = _configuration.GetValue("dig:ChiaCertDirectory", "");
        if (string.IsNullOrEmpty(certificateFolderPath))
        {
            certificateFolderPath = Directory.GetParent(Path.GetDirectoryName(fullNode.KeyPath)!)?.ToString();
        }

        if (!Directory.Exists(certificateFolderPath))
        {
            throw new Exception($"Could not locate the chia cert directory at {certificateFolderPath}");
        }

        args.Add("--certificateFolderPath");
        args.Add(certificateFolderPath!);

        var a = string.Join(" ", args);

        var p = new Process()
        {
            StartInfo = new ProcessStartInfo(programPath, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        p.Start();

        var error = p.StandardError.ReadToEnd();
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("server coin error {error}", p.StandardError.ReadToEnd());
            throw new Exception(error);
        }

        return p.StandardOutput.ReadToEnd();
    }
}
