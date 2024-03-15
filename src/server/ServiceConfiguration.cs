using chia.dotnet;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.Razor;
using dig.caching;
using Microsoft.AspNetCore.DataProtection;

namespace dig.server;

public static class ServiceConfiguration
{
    public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder, AppStorage appStorage)
    {
        if (builder.Environment.IsProduction())
        {
            builder.Services.AddApplicationInsightsTelemetry();
        }

        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.AddService<FooterDataFilter>();
        });
        builder.Services.AddRazorPages();

        builder.Services.AddSingleton<ChiaConfig>()
            .AddHttpClient()
            .AddSingleton((provider) => appStorage)
            .AddHostedService<ServerService>()
            .AddSingleton<GatewayService>()
            .AddSingleton<MirrorService>()
            .AddSingleton<DnsService>()
            .AddSingleton<StoreRegistryService>()
            .AddSingleton<MeshNetworkRoutingService>()
            .AddSingleton<ServerCoinService>()
            .AddSingleton<ChiaService>()
            .AddScoped<FooterDataFilter>()
            .AddScoped<IViewEngine, RazorViewEngine>() // we use this so we can check for the existence of a view by name
            .AddMemoryCache()
            .RegisterChiaEndPoint<DataLayerProxy>("dig.server")
            .RegisterChiaEndPoint<FullNodeProxy>("dig.server")
            .RegisterChiaEndPoint<WalletProxy>("dig.server");

        builder.Services.AddHttpClient("datalayer.storage", c =>
        {
            c.BaseAddress = new Uri(builder.Configuration.GetValue("dig:DataLayerStorageUri", "https://api.datalayer.storage/")!);
        }).AddStandardResilienceHandler();

        //
        // this is where configuration based services are added
        //

        switch (builder.Configuration.GetValue("dig:CacheProvider", "Disabled"))
        {
            case "Memory":
                builder.Services.AddSingleton<IObjectCache, MemoryCacheService>();
                break;
            case "FileSystem":
                builder.Services.AddSingleton<IObjectCache, FileCacheService>();
                break;
            case "Disabled":
                builder.Services.AddSingleton<IObjectCache, NullCacheService>();
                break;
            default:
                throw new NotImplementedException($"Cache provider {builder.Configuration.GetValue("dig: CacheProvider", "FileSystem")} not implemented");
        }

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

        return builder;
    }
}
