using chia.dotnet;
using dig.caching;
using Microsoft.AspNetCore.DataProtection;

namespace dig;

public static class ServiceConfiguration
{
    public static HostApplicationBuilder ConfigureServices(this HostApplicationBuilder builder, AppStorage appStorage)
    {
        // Check if the FileCacheDirectory is set in the configuration
        if (string.IsNullOrEmpty(builder.Configuration.GetValue<string>("dig:FileCacheDirectory")))
        {
            // If not set, set it to the user settings folder
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { { "dig:FileCacheDirectory", Path.Combine(appStorage.UserSettingsFolder, "store-cache") } });
        }

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
            .AddSingleton<IObjectCache, FileCacheService>()
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
