using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.AspNetCore.DataProtection;
using chia.dotnet;
using dig;
using dig.server;
using EasyPipes;

var appStorage = new AppStorage(".dig");
var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Services.AddApplicationInsightsTelemetry();
}

if (OperatingSystem.IsWindows())
{
    LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
}

var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
if (File.Exists(path))
{
    Console.WriteLine($"Loading settings from {path}");
}
builder.Configuration.AddJsonFile(path, optional: true);

// we can take the path to an appsettings.json file as an argument
// if not provided, the default appsettings.json will be used and settings
// will come from there or from environment variables
if (!string.IsNullOrEmpty(args.FirstOrDefault()) && File.Exists(args.First()))
{
    Console.WriteLine($"Loading settings from command line file {args.First()}");
    builder.Configuration.AddJsonFile(args.First(), optional: true);
}
else if (File.Exists(appStorage.UserSettingsFilePath))
{
    Console.WriteLine($"Loading user settings from {appStorage.UserSettingsFilePath}");
    builder.Configuration.AddJsonFile(appStorage.UserSettingsFilePath, optional: true);
}

// this sets up the gateway service
builder.Services.AddControllersWithViews();

// these are the services needed by the gateway
builder.Services.AddSingleton<ChiaConfig>()
    .AddHttpClient()
    .AddSingleton((provider) => appStorage)
    .AddActivatedSingleton<IServer, ServerService>()
    .AddSingleton<GatewayService>()
    .AddSingleton<MirrorService>()
    .AddSingleton<DnsService>()
    .AddSingleton<StoreRegistryService>()
    .AddSingleton<ServerCoinService>()
    .AddSingleton(provider => new DataLayerProxy(provider.GetRequiredKeyedService<IRpcClient>("data_layer"), "dig.server"))
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddMemoryCache();

builder.Services.AddRpcEndpoint("data_layer"); //.AddStandardResilienceHandler();
builder.Services.AddHttpClient("datalayer.storage", c =>
{
    c.BaseAddress = new Uri(builder.Configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage/")!);
}).AddStandardResilienceHandler();

// this sets up the sync service - note that it shares some dependencies with the gateway service
// ChiaConfig, RpcClientHost, and DataLayerProxy
if (builder.Configuration.GetValue("dig:NodeSyncJobEnabled", false))
{
    builder.Services
        .AddHostedService<PeriodicNodeSyncService>()
        .AddSingleton<NodeSyncService>()
        .AddSingleton<ChiaService>()
        .AddSingleton<StoreService>()
        .AddSingleton<DnsService>()
        .AddSingleton(provider => new FullNodeProxy(provider.GetRequiredKeyedService<IRpcClient>("full_node"), "dig.server"))
        .AddRpcEndpoint("full_node");
}

// setup the Dyn Dns service
if (builder.Configuration.GetValue("dig:DynDnsJobEnabled", false))
{
    builder.Services
        .AddHostedService<PeriodicDynDnsService>()
        .AddSingleton<DynDnsService>()
        .AddSingleton<DnsService>()
        .AddSingleton<LoginManager>()
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
if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();
app.UseStaticFiles();

// this is the IPC server that the command line talks to
var server = new Server("dig.server.ipc");
server.RegisterService(app.Services.GetRequiredService<IServer>());
server.Start();

var registry = app.Services.GetRequiredService<StoreRegistryService>();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
Task.Run(() => registry.Refresh()); // run this in the background
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

app.Run();
server.Stop();
