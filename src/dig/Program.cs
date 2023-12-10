using chia.dotnet;
using Microsoft.AspNetCore.DataProtection;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<StoreManager>()
    .AddSingleton<HostManager>()
    .AddSingleton<LoginManager>()
    .AddSingleton<ChiaConfig>()
    .AddSingleton<DnsService>()
    .AddSingleton<RpcClientHost>()
    .AddSingleton<ContextBinder>()
    .AddSingleton<MirrorService>()
    .AddSingleton<ChiaService>()
    .AddSingleton<SyncService>()
    .AddSingleton((provider) => new AppStorage(".data-layer-storage"))
    .AddSingleton((provider) => new DataLayerProxy(provider.GetRequiredService<RpcClientHost>().GetRpcClient("data_layer"), "dig"))
    .AddSingleton((provider) => new WalletProxy(provider.GetRequiredService<RpcClientHost>().GetRpcClient("wallet"), "dig"))
    .AddSingleton((provider) => new FullNodeProxy(provider.GetRequiredService<RpcClientHost>().GetRpcClient("full_node"), "dig"))
    .AddDataProtection()
    .SetApplicationName("data-layer-storage")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(180)); ;

var host = builder.Build();

return await new CommandLineBuilder(new RootCommand("Utilities to manage chia data layer storage, mirrors, and subscriptions."))
    .UseDefaults()
    .UseAttributes(host.Services)
    .Build()
    .InvokeAsync(args);
