namespace dig.cli;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using dig.server;
using Microsoft.AspNetCore.Builder;
using chia.dotnet;
using Microsoft.AspNetCore.DataProtection;

internal sealed class RunCommand()
{
    [Option("s", "settings", IsRequired = false, ArgumentHelpName = "FILE_PATH", Description = "Settings file full path")]
    public string? Settings { get; init; }

    [CommandTarget]
    public async Task<int> Execute()
    {
        var builder = WebApplication.CreateBuilder();

        if (builder.Environment.IsProduction())
        {
            builder.Services.AddApplicationInsightsTelemetry();
        }

        if (OperatingSystem.IsWindows())
        {
            LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
        }

        // we can take the path to an appsettings.json file as an argument
        // if not provided, the default appsettings.json will be used and settings
        // will come from there or from environment variables
        if (Settings is not null)
        {
            var configurationBinder = new ConfigurationBuilder()
                .AddJsonFile(Settings);

            var config = configurationBinder.Build();
            builder.Configuration.AddConfiguration(config);
        }

        // this sets up the gateway service
        builder.Services.AddControllers();

        builder.Services.AddSingleton<ChiaConfig>()
            .AddHttpClient()
            .AddSingleton<GatewayService>()
            .AddSingleton(provider => new DataLayerProxy(provider.GetRequiredKeyedService<IRpcClient>("data_layer"), "dig.server"))
            .AddEndpointsApiExplorer()
            .AddSwaggerGen()
            .AddMemoryCache();

        builder.Services.AddRpcEndpoint("data_layer").AddStandardResilienceHandler();
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
                .AddSingleton((provider) => new AppStorage(".distributed-internet-gateway"))
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
        app.UseCors();
        app.MapControllers();

        await app.RunAsync();
        return 0;
    }
}
