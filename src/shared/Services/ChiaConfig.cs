using chia.dotnet;

namespace dig;

/// <summary>
/// Provides methods for managing chia connection details.
/// </summary>
public sealed class ChiaConfig
{
    private readonly ILogger<ChiaConfig> _logger;
    private readonly IConfiguration _configuration;
    private readonly Config? _config;

    public ChiaConfig(ILogger<ChiaConfig> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _config = GetConfig();
    }

    public EndpointInfo? GetEndpoint(string name)
    {
        //
        // There is an order of precedence for where an endpoint config can come from
        //
        // 1. User secrets
        // 2. Appsettings.json
        // 3. A chia config file located byt the GetConfig() method
        //

        // 1. check user secrets for the connection
        // https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-8.0&tabs=windows
        var endpointUri = _configuration.GetValue($"{name}_uri", "")!;
        if (!string.IsNullOrEmpty(endpointUri))
        {
            _logger.LogInformation("Connecting to {endpointUri}", endpointUri);

            return new EndpointInfo()
            {
                Uri = new Uri(endpointUri),
                // when stored in an environment variable the newlines might be escaped
                Cert = _configuration.GetValue($"{name}_cert", "")!.Replace("\\n", "\n"),
                Key = _configuration.GetValue($"{name}_key", "")!.Replace("\\n", "\n")
            };
        }

        // 2. check appsettings.json for the connection
        var endpoint = GetEndpointFromAppSettings(name);
        if (endpoint != null)
        {
            return endpoint;
        }

        // 3. check the chia config file for the connection
        if (_config != null)
        {
            // if not present see if we can get it from the config file
            return _config.GetEndpoint(name);
        }

        // couldn't find an endpoint config anywhere
        // must be dealt with in calling code
        return null;
    }

    private EndpointInfo? GetEndpointFromAppSettings(string name)
    {
        if (name == "full_node")
        {
            return BuildEndpointInfo(name, "FullNode", 8555);
        }

        if (name == "wallet")
        {
            return BuildEndpointInfo(name, "Wallet", 9256);
        }

        if (name == "data_layer")
        {
            return BuildEndpointInfo(name, "DataLayer", 8562);
        }

        return null;
    }

    private EndpointInfo? BuildEndpointInfo(string snakeCaseName, string properCaseName, int defaultPort)
    {
        var host = _configuration.GetValue($"dig:{properCaseName}Host", "");
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var port = _configuration.GetValue($"dig:{properCaseName}Port", defaultPort);
        return new EndpointInfo
        {
            Uri = new Uri($"https://{host}:{port}"),
            CertPath = Path.Combine(_configuration.GetValue("dig:CertDirectory", "")!, $"{snakeCaseName}/private_{snakeCaseName}.crt"),
            KeyPath = Path.Combine(_configuration.GetValue("dig:CertDirectory", "")!, $"{snakeCaseName}/private_{snakeCaseName}.key"),
        };
    }

    public string? GetConfigPath()
    {
        // first see if we have a config file path from the command line
        var configPath = _configuration.GetValue("ChiaConfigPath", "");
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            _logger.LogInformation("Using chia config from command line {configPath}", configPath);

            return configPath;
        }

        // then see if we have a config file path in the appsettings.json or environment variable
        configPath = _configuration.GetValue("dig:ChiaConfigPath", "");
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            _logger.LogInformation("Using chia config from configuration {configPath}", configPath);

            return configPath;
        }

        // and finally default to the chia root environment variable or user home
        _logger.LogWarning("Using default chia config path that will be CHIA_ROOT or user home rooted.");

        return null;
    }

    private Config? GetConfig()
    {
        try
        {
            // first see if we have a config file path in the appsettings.json
            var configPath = GetConfigPath();
            if (!string.IsNullOrEmpty(configPath))
            {
                _logger.LogInformation("Using chia config {configPath}", configPath);

                return Config.Open(configPath);
            }

            // this will throw if there is no chia_root or no ~/.chia directory for the server user
            return Config.Open();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open chia config");
        }

        return null;
    }
}
