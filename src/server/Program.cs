using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using dig;
using dig.server;

var appStorage = new AppStorage(".dig");
var builder = WebApplication.CreateBuilder(args);

// this block looks for three settings files in order of precedence:
// 1. appsettings.json in the app's base directory
// 2. appsettings.json in the user's settings directory
// 3. appsettings.user.json in the user's settings directory
builder.Configuration
    .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(appStorage.UserSettingsFolder, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(appStorage.UserSettingsFolder, "appsettings.user.json"), optional: true);

// we can take the path to an appsettings.json file as an argument
// which take precedence over the user settings
if (!string.IsNullOrEmpty(args.FirstOrDefault()) && File.Exists(args.First()))
{
    Console.WriteLine($"Loading settings from command line file {args.First()}");
    builder.Configuration.AddJsonFile(args.First(), optional: true);
}

// Configure CORS to allow Referer header
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowRefererHeader", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .WithExposedHeaders("Referer");
    });
});

builder.ConfigureServices(appStorage);

if (OperatingSystem.IsWindows())
{
    LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
}

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}")
    .UseHttpsRedirection()
    .UseStaticFiles()
    .UseRouting()
    .UseCors("AllowRefererHeader") // Apply the CORS policy
#pragma warning disable ASP0014
    .UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
    });
#pragma warning restore ASP0014

app.Run(); // see the ServerService for various tasks that start here
