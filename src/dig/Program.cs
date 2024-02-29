using chia.dotnet;
using dig;
using Microsoft.AspNetCore.DataProtection;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

var appStorage = new AppStorage(".dig");
var builder = Host.CreateApplicationBuilder(args);

// the non-web app builder doesn't bind to settings files automatically
var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
builder.Configuration.AddJsonFile(path, optional: true);
builder.Configuration.AddJsonFile(appStorage.UserSettingsFilePath, optional: true);

// configure services
builder.Services.AddSingleton<StoreManager>()
    .AddSingleton<DynDnsService>()
    .AddSingleton<HostManager>()
    .AddSingleton<LoginManager>()
    .AddSingleton<ChiaConfig>()
    .AddSingleton<DnsService>()
    .AddSingleton<ContextBinder>()
    .AddSingleton<NodeSyncService>()
    .AddSingleton<ChiaService>()
    .AddSingleton<StoreService>()
    .AddSingleton<ServerCoinService>()
    .AddSingleton((provider) => appStorage)
    .AddHttpClient()
    .AddSingleton(provider => new DataLayerProxy(provider.GetRequiredKeyedService<IRpcClient>("data_layer"), "dig.server"))
    .AddSingleton(provider => new FullNodeProxy(provider.GetRequiredKeyedService<IRpcClient>("full_node"), "dig.server"))
    .AddDataProtection()
    .SetApplicationName("distributed-internet-gateway")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(180)); ;

builder.Services.AddHttpClient("datalayer.storage", c =>
    {
        c.BaseAddress = new Uri(builder.Configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage")!);
    });

builder.Services.AddRpcEndpoint("data_layer");
builder.Services.AddRpcEndpoint("full_node");

var host = builder.Build();

return await new CommandLineBuilder(new RootCommand("Utilities to manage the chia data layer distributed internet gateway."))
    .UseDefaults()
    .UseAttributes(host.Services) // this binds the command line parser to the DI container and creates the command tree
    .Build()
    .InvokeAsync(args);
