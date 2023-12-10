using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using chia.dotnet;

// doing all of this in the mini-api expressjs-like approach
// instead of the IActionResult approach

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Services.AddApplicationInsightsTelemetry();
}

if (OperatingSystem.IsWindows())
{
    LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
}

// we can take the path to an appsettings.json file as an argument
// if not provided, the default appsettings.json will be used and settings
// will come from there or from environment variables
if (args.Length != 0)
{
    var configurationBinder = new ConfigurationBuilder()
        .AddJsonFile(args.First());

    var config = configurationBinder.Build();
    builder.Configuration.AddConfiguration(config);
}

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddSingleton<ChiaConfig>()
    .AddSingleton<RpcClientHost>()
    .AddSingleton((provider) => new DataLayerProxy(provider.GetRequiredService<RpcClientHost>().GetRpcClient("data_layer"), "dig.server"))
    .AddSingleton<G2To3Service>()
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddMemoryCache();

var app = builder.Build();
var configuration = app.Configuration;
if (OperatingSystem.IsWindows() && configuration.GetValue("App:windows_service", false))
{
    builder.Host.UseWindowsService();
}

app.UseCors();
app.MapControllers();
app.Run();
