using chia.dotnet;
using Microsoft.AspNetCore.DataProtection;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<StoreManager>()
    .AddSingleton<DynDnsService>()
    .AddSingleton<HostManager>()
    .AddSingleton<LoginManager>()
    .AddSingleton<ChiaConfig>()
    .AddSingleton<DnsService>()
    .AddSingleton<ContextBinder>()
    .AddSingleton<MirrorService>()
    .AddSingleton<ChiaService>()
    .AddSingleton<StoreSyncService>()
    .AddSingleton((provider) => new AppStorage(".distributed-internet-gateway"))
    .AddHttpClient()
    .AddSingleton(provider => new WalletProxy(provider.GetRequiredKeyedService<IRpcClient>("wallet"), "dig.server"))
    .AddSingleton(provider => new DataLayerProxy(provider.GetRequiredKeyedService<IRpcClient>("data_layer"), "dig.server"))
    .AddSingleton(provider => new FullNodeProxy(provider.GetRequiredKeyedService<IRpcClient>("full_node"), "dig.server"))
    .AddDataProtection()
    .SetApplicationName("distributed-internet-gateway")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(180)); ;

builder.Services.AddHttpClient("datalayer.storage", c =>
    {
        c.BaseAddress = new Uri(builder.Configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage")!);
    });

builder.Services.AddRpcEndpoint("wallet");
builder.Services.AddRpcEndpoint("data_layer");
builder.Services.AddRpcEndpoint("full_node");

var host = builder.Build();

return await new CommandLineBuilder(new RootCommand("Utilities to manage the chia data layer distributed internet gateway."))
    .UseDefaults()
    .UseAttributes(host.Services) // this binds the command line parser to the DI container and creates the command tree
    .Build()
    .InvokeAsync(args);
