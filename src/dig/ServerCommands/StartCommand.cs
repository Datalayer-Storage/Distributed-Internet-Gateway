namespace dig;

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

internal sealed class StartCommand()
{
    [Option("s", "settings", ArgumentHelpName = "FILE_PATH", Description = "Full path to a settings file.")]
    public string? Settings { get; init; }

    [CommandTarget]
    public async Task<int> Execute(ILogger<StartCommand> logger)
    {

        if (Settings is not null && !File.Exists(Settings))
        {
            logger.LogError("Settings file not found {path}.", Settings);
            return -1;
        }

        if (await ServerProcess.GetIsRunning())
        {
            logger.LogError("Server is already running.");
            return -1;
        }

        try
        {
            ServerProcess.Start(Settings);
            logger.LogInformation("Server started.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start server.");
            return -1;
        }

        return 0;
    }
}
