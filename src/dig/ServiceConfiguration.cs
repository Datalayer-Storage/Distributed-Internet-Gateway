using chia.dotnet;
using dig.caching;
using Microsoft.AspNetCore.DataProtection;

namespace dig;

public static class ServiceConfiguration
{
    public static HostApplicationBuilder ConfigureServices(this HostApplicationBuilder builder, AppStorage appStorage)
    {
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
            .AddSingleton<StorePreCacheService>()
            .AddSingleton<IObjectCache, FileCacheService>()
            .AddSingleton<ServerCoinService>()
            .AddSingleton((provider) => appStorage)
            .AddHttpClient()
            .RegisterChiaEndPoint<DataLayerProxy>("dig.node")
            .RegisterChiaEndPoint<FullNodeProxy>("dig.node")
            .RegisterChiaEndPoint<WalletProxy>("dig.node")
            .AddDataProtection()
            .SetApplicationName("distributed-internet-gateway")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(180));

        builder.Services.AddHttpClient("datalayer.storage", c =>
            {
                c.BaseAddress = new Uri(builder.Configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage")!);
            });

        return builder;
    }
}
