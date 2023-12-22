namespace dig;

using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class ServerProcess
{
    public static void Start(string? settings)
    {
        string programName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dig.server.exe" : "dig.server";
        string programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, programName);

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = programPath,
                UseShellExecute = true, // This will make the process not be a child process
                Arguments = settings is not null ? $"\"{settings}\"" : string.Empty
            }
        };

        process.Start();
    }

    public static async Task<bool> GetIsRunning()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "dig.server.ipc", PipeDirection.InOut);
            client.Connect(TimeSpan.FromSeconds(1));
            using var writer = new StreamWriter(client);
            await writer.WriteLineAsync("hello");
            await writer.FlushAsync();
            using StreamReader reader = new(client);
            var line = await reader.ReadLineAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
