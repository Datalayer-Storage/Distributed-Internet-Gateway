namespace dig;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Dynamic;
using Newtonsoft.Json;

public class ServerCoinService(ChiaConfig chiaConfig,
                                        ILogger<ServerCoinService> logger,
                                        IConfiguration configuration)
{
    private readonly ChiaConfig _chiaConfig = chiaConfig;
    private readonly ILogger<ServerCoinService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public bool AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee)
    {
        List<string> args = ["add_server", "--storeId", storeId, "--url", serverUrl, "--amount", mojoReserveAmount.ToString(), "--fee", fee.ToString()];

        var result = RunCommand(args).Trim();

        return result == "true";
    }

    public string DeleteServer(string storeId, string coinId, ulong fee)
    {
        List<string> args = ["delete_server", "--storeId", storeId, "--coinId", coinId, "--fee", fee.ToString()];

        return RunCommand(args);
    }

    public IEnumerable<dynamic> GetCoins(string storeId)
    {
        List<string> args = ["get_server_coins", "--storeId", storeId];

        var json = RunCommand(args);
        if (!string.IsNullOrEmpty(json))
        {
            dynamic result = JsonConvert.DeserializeObject<ExpandoObject>(json) ?? throw new Exception("Failed to parse server coin result");

            return result.servers;
        }

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
            args.Add("--fullNodeHost");
            args.Add(fullNode.Uri.Host);
        } 
        else
        {
            args.Add("--autoFindPeer");
            args.Add("true");
        }

        // server_coin error if passed non-default ports

        
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
