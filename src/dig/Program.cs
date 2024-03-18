using dig;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

var appStorage = new AppStorage(".dig");
var builder = Host.CreateApplicationBuilder(args);

// the non-web app builder doesn't bind to settings files automatically
// this block looks for three settings files in order of precedence:
// 1. appsettings.json in the app's base directory
// 2. appsettings.json in the user's settings directory
// 3. appsettings.user.json in the user's settings directory
builder.Configuration
    .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(appStorage.UserSettingsFolder, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(appStorage.UserSettingsFolder, "appsettings.user.json"), optional: true);

if (OperatingSystem.IsWindows())
{
    LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
}

var host = builder.ConfigureServices(appStorage)
    .Build();

return await new CommandLineBuilder(new RootCommand("Utilities to manage the chia data layer distributed internet gateway."))
    .UseDefaults()
    .UseAttributes(host.Services) // this binds the command line parser to the DI container and creates the command tree
    .Build()
    .InvokeAsync(args);
