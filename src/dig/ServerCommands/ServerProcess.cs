namespace dig;

using System.IO;
using EasyPipes;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class ServerProcess
{
    public static void Start(string? settingsFilePath)
    {
        string programName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dig.server.exe" : "dig.server";
        string programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, programName);

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = programPath,
                UseShellExecute = true, // This will prevent creating a child process
                Arguments = settingsFilePath is not null ? $"\"{settingsFilePath}\"" : string.Empty
            }
        };

        process.Start();
    }

    public static void Stop()
    {
        var client = new Client("dig.server.ipc");
        var server = client.GetServiceProxy<IServer>();

        server.Stop();
    }

    public static bool GetIsRunning()
    {
        try
        {
            var client = new Client("dig.server.ipc");
            var server = client.GetServiceProxy<IServer>();

            return server.Ping();
        }
        catch
        {
            return false;
        }
    }
}
