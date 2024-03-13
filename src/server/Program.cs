using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using dig;
using dig.server;

var appStorage = new AppStorage(".dig");
var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows())
{
    LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
}

var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
if (File.Exists(path))
{
    Console.WriteLine($"Loading settings from {path}");
    builder.Configuration.AddJsonFile(path, optional: true);
}

// we can take the path to an appsettings.json file as an argument
// if not provided, the default appsettings.json will be used and settings
// will come from there or from environment variables
if (!string.IsNullOrEmpty(args.FirstOrDefault()) && File.Exists(args.First()))
{
    Console.WriteLine($"Loading settings from command line file {args.First()}");
    builder.Configuration.AddJsonFile(args.First(), optional: true);
}
else if (File.Exists(appStorage.UserSettingsFilePath))
{
    Console.WriteLine($"Loading user settings from {appStorage.UserSettingsFilePath}");
    builder.Configuration.AddJsonFile(appStorage.UserSettingsFilePath, optional: true);
}

builder.ConfigureServices(appStorage);

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}")
    .UseHttpsRedirection()
    .UseStaticFiles()
    .UseRouting()
    .UseCors()
#pragma warning disable ASP0014
    .UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
    });
#pragma warning restore ASP0014

app.Run(); // see the ServerService for various tasks that start here
