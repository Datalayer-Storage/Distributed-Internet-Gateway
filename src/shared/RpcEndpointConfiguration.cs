using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using chia.dotnet;

namespace dig;

internal static class RpcEndpointConfiguration
{
    public static IServiceCollection RegisterChiaEndPoint<T>(this IServiceCollection services, string applicationName) where T : ServiceProxy
    {
        var endpointName = typeof(T) == typeof(DataLayerProxy) ? "data_layer" :
                           typeof(T) == typeof(FullNodeProxy) ? "full_node" :
                           typeof(T) == typeof(WalletProxy) ? "wallet" :
                           throw new InvalidOperationException("Unknown chia service type");

        return services.AddRpcEndpoint(endpointName)
            .AddSingleton(provider =>
            {
                var rpcClient = provider.GetRequiredKeyedService<IRpcClient>(endpointName);
                return Activator.CreateInstance(typeof(T), rpcClient, applicationName) as T ?? throw new InvalidOperationException("Could not create service proxy");
            });
    }

    // this overrides the HttpHandler that chia.dotnet uses internally
    // and allows integration with aspnet core hosting and resilience providers
    private static IServiceCollection AddRpcEndpoint(this IServiceCollection services, string name)
    {
        // add a keyed singleton for the IRpcClient
        services.AddKeyedSingleton<IRpcClient>(name, (provider, key) =>
        {
            var chiaConfig = provider.GetRequiredService<ChiaConfig>();
            var endpoint = chiaConfig.GetEndpoint(name) ?? new EndpointInfo();

            var client = provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(name);

            return new HttpRpcClient(endpoint, client);
        });

        // register a named client with the IHttpFactory
        // and configure it to use a custom handler
        _ = services.AddHttpClient(name, (provider, client) =>
        {
            var timeout = provider.GetRequiredService<IConfiguration>().GetValue("dig:RpcTimeoutSeconds", 60);
            var chiaConfig = provider.GetRequiredService<ChiaConfig>();
            var endpoint = chiaConfig.GetEndpoint(name) ?? new EndpointInfo();

            client.BaseAddress = endpoint.Uri;
            client.Timeout = TimeSpan.FromSeconds(timeout);
        })
        .UseSocketsHttpHandler((socketHandler, provider) =>
        {
            socketHandler.PooledConnectionLifetime = TimeSpan.FromMinutes(15);
            socketHandler.SslOptions.RemoteCertificateValidationCallback += ValidateServerCertificate;

            var chiaConfig = provider.GetRequiredService<ChiaConfig>();
            var endpoint = chiaConfig.GetEndpoint(name);
            if (endpoint is not null)
            {
                socketHandler.SslOptions.ClientCertificates = endpoint.GetCert();
            }
        });

        return services;
    }

    private static bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        // uncomment these checks to change remote cert validation requirements

        // require remote ca to be trusted on this machine
        //if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.RemoteCertificateChainErrors)
        //    return false;

        // require server name to be validated in the cert
        //if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) == SslPolicyErrors.RemoteCertificateNameMismatch)
        //    return false;

        return !((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) == SslPolicyErrors.RemoteCertificateNotAvailable);
    }
}
