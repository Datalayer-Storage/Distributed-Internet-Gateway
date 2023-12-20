namespace dig.cli;

internal sealed class ShowConfigCommand()
{
    [Option("t", "timeout", Default = 60, ArgumentHelpName = "SECONDS", Description = "Timeout in seconds")]
    public int Timeout { get; init; } = 60;

    [CommandTarget]
    public async Task<int> Execute(ChiaConfig chiaConfig, DnsService dnsService)
    {
        var chiaRoot = Environment.GetEnvironmentVariable("CHIA_ROOT") ?? "<Not set>";
        Console.WriteLine($"CHIA_ROOT: {chiaRoot}");

        var configPath = chiaConfig.GetConfigPath() ?? "<Not set>";
        Console.WriteLine($"Chia root path setting: {configPath}");

        var config = chiaConfig.GetConfig();
        Console.WriteLine($"Chia root path actual: {config.ChiaRootPath}");

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
        var hostIp = await dnsService.GetPublicIPAdress(cancellationSource.Token);
        hostIp ??= "<error>";
        Console.WriteLine($"hostIp: {hostIp}");

        var hostUri = await dnsService.GetHostUri(cancellationSource.Token);
        hostUri ??= "<error>";
        Console.WriteLine($"hostUri: {hostUri}");

        return 0;
    }
}
