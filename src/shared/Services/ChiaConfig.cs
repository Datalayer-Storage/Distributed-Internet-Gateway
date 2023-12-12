using chia.dotnet;

/// <summary>
/// Provides methods for managing chia connection details.
/// </summary>
internal sealed class ChiaConfig(ILogger<ChiaConfig> logger, IConfiguration configuration)
{
    private readonly ILogger<ChiaConfig> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public string? GetConfigPath()
    {
        // first see if we have a config file path from the command line
        var configPath = _configuration.GetValue("ChiaConfigPath", "");
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }

        // then see if we have a config file path in the appsettings.json or environment variable
        configPath = _configuration.GetValue("dig:ChiaConfigPath", "");
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }

        return null;
    }

    public Config GetConfig()
    {
        // first see if we have a config file path in the appsettings.json
        var configPath = GetConfigPath();
        if (!string.IsNullOrEmpty(configPath))
        {
            _logger.LogInformation("Using chia config {configPath}", configPath);

            return Config.Open(configPath);
        }

        return Config.Open();
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
        else
        {
            // if not present see if we can get it from the config file
            return GetConfig().GetEndpoint(name);
        }
    }
}
