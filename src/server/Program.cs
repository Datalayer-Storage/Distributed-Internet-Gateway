using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.AspNetCore.DataProtection;
using chia.dotnet;

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

// this sets up the gateway service
builder.Services.AddSingleton<ChiaConfig>()
    .AddHttpClient()
    .AddSingleton<G2To3Service>()
    .AddSingleton(provider => new DataLayerProxy(provider.GetRequiredKeyedService<IRpcClient>("data_layer"), "dig.server"))
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddMemoryCache()
    .AddControllers();

builder.Services.AddRpcEndpoint("data_layer");
builder.Services.AddHttpClient("datalayer.storage", c =>
{
    c.BaseAddress = new Uri(builder.Configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage/")!);
}).AddStandardResilienceHandler();

// this sets up the sync service - note that it shares some dependencies with the gateway service
// ChiaConfig, RpcClientHost, and DataLayerProxy
if (builder.Configuration.GetValue("dig:RunMirrorSyncJob", false))
{
    builder.Services
        .AddHostedService<PeriodicStoreSyncService>()
        .AddSingleton<ChiaService>()
        .AddSingleton<MirrorService>()
        .AddSingleton<StoreSyncService>()
        .AddSingleton<DnsService>()
        .AddSingleton(provider => new FullNodeProxy(provider.GetRequiredKeyedService<IRpcClient>("full_node"), "dig.server"))
        .AddSingleton(provider => new WalletProxy(provider.GetRequiredKeyedService<IRpcClient>("wallet"), "dig.server"));

    builder.Services.AddRpcEndpoint("wallet");
    builder.Services.AddRpcEndpoint("full_node");
}

// setup the Dyn Dns service
if (builder.Configuration.GetValue("dig:RunDynDnsJob", false))
{
    builder.Services
        .AddHostedService<PeriodicDynDnsService>()
        .AddSingleton<DynDnsService>()
        .AddSingleton<DnsService>()
        .AddSingleton<LoginManager>()
        .AddSingleton((provider) => new AppStorage(".distributed-internet-gateway"))
        .AddDataProtection()
        .SetApplicationName("distributed-internet-gateway")
        .SetDefaultKeyLifetime(TimeSpan.FromDays(180));
}

if (builder.Configuration.GetValue("dig:RunAsWindowsService", false))
{
    builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Distributed Internet Gateway";
        });
    builder.Host.UseWindowsService(); // safe to call on non-windows platforms
}

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Run();
