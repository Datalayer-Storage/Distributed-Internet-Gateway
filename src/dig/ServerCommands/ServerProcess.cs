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
        if (!File.Exists(programPath))
        {
            throw new Exception($"Could not locate the server executable at {programPath}");
        }

        var p = new Process()
        {
            StartInfo = new ProcessStartInfo(programPath, settingsFilePath!)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        p.Start();
        Thread.Sleep(5000); // wait for the server to start to see if it is successful
        if (p.HasExited)
        {
            throw new Exception($"The server failed to start.\n{p.StandardOutput.ReadToEnd()}");
        }
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
