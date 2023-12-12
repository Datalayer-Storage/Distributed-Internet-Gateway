using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using chia.dotnet;

internal static class RpcEndpointConfiguration
{
    // this overrides the HttpHandler that chia.dotnet uses internally
    // and allows integration with aspnet core hosting and resilience providers
    public static IHttpClientBuilder AddRpcEndpoint(this IServiceCollection services, string name)
    {
        // add a keyed singleton for the IRpcClient
        services.AddKeyedSingleton<IRpcClient>(name, (provider, key) =>
        {
            var chiaConfig = provider.GetRequiredService<ChiaConfig>();
            var endpoint = chiaConfig.GetEndpoint(name);

            var client = provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(name);

            return new HttpRpcClient(endpoint, client);
        });

        // register a named client with the IHttpFactory
        // and configure it to use a custom handler
        return services.AddHttpClient(name, (provider, client) =>
        {
            var timeout = provider.GetRequiredService<IConfiguration>().GetValue("dig:RpcTimeoutSeconds", 30);
            var chiaConfig = provider.GetRequiredService<ChiaConfig>();
            var endpoint = chiaConfig.GetEndpoint(name);

            client.BaseAddress = endpoint.Uri;
            client.Timeout = TimeSpan.FromSeconds(timeout);
        })
        .UseSocketsHttpHandler((socketHandler, provider) =>
        {
            var chiaConfig = provider.GetRequiredService<ChiaConfig>();
            var endpoint = chiaConfig.GetEndpoint(name);

            socketHandler.PooledConnectionLifetime = TimeSpan.FromMinutes(15);
            socketHandler.SslOptions.ClientCertificates = endpoint.GetCert();
            socketHandler.SslOptions.RemoteCertificateValidationCallback += ValidateServerCertificate;
        });
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
