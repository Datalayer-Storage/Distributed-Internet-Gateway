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

#if DEBUG
        // when installed the cli and server binaries are in the same folder
        // from the ide the server is in the project folder tree
        if (!File.Exists(programPath))
        {
            programPath = programPath.Replace($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}dig", $"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}server");
        }
#endif
        var p = Process.Start(programPath, settingsFilePath!);
        // using Process process = new()
        // {
        //     StartInfo = new ProcessStartInfo
        //     {
        //         FileName = programPath,
        //         UseShellExecute = true, // This will prevent creating a child process
        //         Arguments = settingsFilePath is not null ? $"\"{settingsFilePath}\"" : string.Empty
        //     }
        // };

        // var started = process.Start();
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
