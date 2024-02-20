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
builder.Configuration.AddJsonFile(path, optional: true);

// we can take the path to an appsettings.json file as an argument
// if not provided, the default appsettings.json will be used and settings
// will come from there or from environment variables
if (args.Length != 0 && !string.IsNullOrEmpty(args.First()))
{
    if (File.Exists(args.First()))
    {
        var configurationBinder = new ConfigurationBuilder().AddJsonFile(args.First());
        var config = configurationBinder.Build();
        builder.Configuration.AddConfiguration(config);
    }
    else
    {
        Console.WriteLine($"WARNING: The file {args.First()} does not exist. Ignoring command line argument.");
    }
}

// this sets up the gateway service
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<ChiaConfig>()
    .AddHttpClient()
    .AddActivatedSingleton<IServer, ServerService>()
    .AddSingleton<GatewayService>()
    .AddSingleton<DnsService>()
    .AddSingleton<MirrorService>()
    .AddSingleton<StoreRegistryService>()
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
if (builder.Configuration.GetValue("dig:RunMirrorSyncJob", false))
{
    builder.Services
        .AddHostedService<PeriodicStoreSyncService>()
        .AddSingleton<ChiaService>()
        .AddSingleton<MirrorService>()
        .AddSingleton<StoreSyncService>()
        .AddSingleton<DnsService>()
        .AddSingleton(provider => new FullNodeProxy(provider.GetRequiredKeyedService<IRpcClient>("full_node"), "dig.server"))
        .AddRpcEndpoint("full_node");
}

// setup the Dyn Dns service
if (builder.Configuration.GetValue("dig:RunDynDnsJob", false))
{
    builder.Services
        .AddHostedService<PeriodicDynDnsService>()
        .AddSingleton<DynDnsService>()
        .AddSingleton<DnsService>()
        .AddSingleton<LoginManager>()
        .AddSingleton((provider) => appStorage)
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();
app.UseStaticFiles();

// this is the IPC server that the command line talk to
var server = new Server("dig.server.ipc");
server.RegisterService(app.Services.GetRequiredService<IServer>());
server.Start();

var registry = app.Services.GetRequiredService<StoreRegistryService>();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
Task.Run(() => registry.Refresh()); // run this in the background
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

app.Run();
server.Stop();
