using chia.dotnet;

namespace dig;

/// <summary>
/// Provides methods for managing chia connection details.
/// </summary>
public sealed class ChiaConfig(ILogger<ChiaConfig> logger, IConfiguration configuration)
{
    private readonly ILogger<ChiaConfig> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private Config? _config;

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

    public Config? GetConfig()
    {
        if (_config != null)
        {
            return _config;
        }

        try
        {
            // first see if we have a config file path in the appsettings.json
            var configPath = GetConfigPath();
            if (!string.IsNullOrEmpty(configPath))
            {
                _logger.LogInformation("Using chia config {configPath}", configPath);

                _config = Config.Open(configPath);
            }

            // this will throw if there is no chia_root or no ~/.chia directory for the server user
            _config = Config.Open();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open chia config");
        }

        return _config;
    }

    public EndpointInfo GetEndpoint(string name)
    {
        // first check user secrets for the connection
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
        else if (GetConfig() != null)
        {
            // if not present see if we can get it from the config file
            return GetConfig()!.GetEndpoint(name);
        }

        // couldn't find a config so just return an empty endpoint
        return new EndpointInfo();
    }
}
