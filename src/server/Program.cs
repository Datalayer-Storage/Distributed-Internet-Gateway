using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.AspNetCore.DataProtection;
using chia.dotnet;
using dig;
using dig.server;

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

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// this sets up the gateway service
// these are the services needed by the gateway
builder.Services.AddSingleton<ChiaConfig>()
    .AddHttpClient()
    .AddSingleton((provider) => appStorage)
    .AddHostedService<ServerService>()
    .AddSingleton<StoreCacheService>()
    .AddSingleton<GatewayService>()
    .AddSingleton<MirrorService>()
    .AddSingleton<DnsService>()
    .AddSingleton<StoreRegistryService>()
    .AddSingleton<MeshNetworkRoutingService>()
    .AddSingleton<ServerCoinService>()
    .AddSingleton<ChiaService>()
    .AddSingleton<FileCacheService>()
    .AddMemoryCache()
    .RegisterChiaEndPoint<DataLayerProxy>("dig.server")
    .RegisterChiaEndPoint<FullNodeProxy>("dig.server")
    .RegisterChiaEndPoint<WalletProxy>("dig.server");

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
        .AddSingleton<StoreService>()
        .AddSingleton<DnsService>();
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

app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
#pragma warning disable ASP0014
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
#pragma warning restore ASP0014

app.Run(); // see the ServerService for various tasks that start here
