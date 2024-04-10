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
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { { "dig:FileCacheDirectory", Path.Combine(appStorage.UserSettingsFolder, "cache") } });
        }
        if (string.IsNullOrEmpty(builder.Configuration.GetValue<string>("dig:ObjectStoreDirectory")))
        {
            // If not set, set it to the user settings folder
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { { "dig:ObjectStoreDirectory", Path.Combine(appStorage.UserSettingsFolder, "objects") } });
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
            .AddSingleton<IObjectStore, FileObjectStore>()
            .AddSingleton<StorePreCacheService>()
            .AddSingleton<IServerCoinService, ServerCoinCliService>()
            .AddSingleton((provider) => appStorage)
            .AddHttpClient()
            .RegisterChiaEndPoint<DataLayerProxy>("dig.node")
            .RegisterChiaEndPoint<FullNodeProxy>("dig.node")
            .RegisterChiaEndPoint<WalletProxy>("dig.node")
            .AddDataProtection()
            .SetApplicationName("distributed-internet-gateway")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(180));

        if (builder.Configuration.GetValue("dig:ServerCoinServiceProvider", "ServerCoinService") == "ServerCoinService")
        {
            builder.Services.AddSingleton<IServerCoinService, ServerCoinService>();
        }
        else
        {
            builder.Services.AddSingleton<IServerCoinService, ServerCoinCliService>();
        }

        builder.Services.AddHttpClient("datalayer.storage", c =>
            {
                c.BaseAddress = new Uri(builder.Configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage")!);
            });

        return builder;
    }
}
