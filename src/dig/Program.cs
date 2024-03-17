using dig;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

var appStorage = new AppStorage(".dig");
var builder = Host.CreateApplicationBuilder(args);

// the non-web app builder doesn't bind to settings files automatically
var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

// this looks for appsettings.user.json in the user's ~/.dig directory
builder.Configuration.AddJsonFile(path, optional: true)
    .AddJsonFile(appStorage.UserSettingsFilePath, optional: true);

var host = builder.ConfigureServices(appStorage)
    .Build();

return await new CommandLineBuilder(new RootCommand("Utilities to manage the chia data layer distributed internet gateway."))
    .UseDefaults()
    .UseAttributes(host.Services) // this binds the command line parser to the DI container and creates the command tree
    .Build()
    .InvokeAsync(args);
