using chia.dotnet;
using Microsoft.AspNetCore.DataProtection;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<StoreManager>()
    .AddSingleton<HostManager>()
    .AddSingleton<LoginManager>()
    .AddSingleton<ChiaConfig>()
    .AddSingleton<DnsService>()
    .AddSingleton<RpcClientHost>()
    .AddSingleton<ContextBinder>()
    .AddSingleton<MirrorService>()
    .AddSingleton<ChiaService>()
    .AddSingleton<StoreSyncService>()
    .AddSingleton((provider) => new AppStorage(".distributed-internet-gateway"))
    .AddSingleton((provider) => new DataLayerProxy(provider.GetRequiredService<RpcClientHost>().GetRpcClient("data_layer"), "dig"))
    .AddSingleton((provider) => new WalletProxy(provider.GetRequiredService<RpcClientHost>().GetRpcClient("wallet"), "dig"))
    .AddSingleton((provider) => new FullNodeProxy(provider.GetRequiredService<RpcClientHost>().GetRpcClient("full_node"), "dig"))
    .AddDataProtection()
    .SetApplicationName("distributed-internet-gateway")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(180)); ;

var host = builder.Build();

return await new CommandLineBuilder(new RootCommand("Utilities to manage the chia data layer distributed internet gateway."))
    .UseDefaults()
    .UseAttributes(host.Services) // this binds the command line parser to the DI container and creates the command tree
    .Build()
    .InvokeAsync(args);
