using System.Runtime.InteropServices;

namespace dig;

public class AppStorage
{
    private readonly string _folderPath = string.Empty;

    public AppStorage(string folderName)
    {
        try
        {
            if (IsRunningAsService())
            {
                // when running as a service, redirect the user folder to temp
                // which effectively means that there is no user settings or that
                // the path to user settings have been passed as an argument to the service
                // The file cache service will cache to temp as well
                //
                // this is a fallback for when the user settings are not available or set
                _folderPath = Path.Combine(Path.GetTempPath(), folderName);
            }
            else
            {
                // the default folder is the user's profile folder
                // e.g. C:\Users\username\folderName or /home/username/folderName
                _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), folderName);
            }

            if (!Directory.Exists(UserSettingsFolder))
            {
                Directory.CreateDirectory(UserSettingsFolder);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    public string UserSettingsFilePath => Path.Combine(UserSettingsFolder, "appsettings.user.json");
    public string UserSettingsFolder => _folderPath;

    public static bool IsRunningAsService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows-specific check
            var userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            return userName.Equals(@"NT AUTHORITY\SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                   userName.Equals(@"NT AUTHORITY\NETWORK SERVICE", StringComparison.OrdinalIgnoreCase) ||
                   userName.Equals(@"NT AUTHORITY\LOCAL SERVICE", StringComparison.OrdinalIgnoreCase);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Linux-specific check - simplistic for known service accounts
            var userName = Environment.UserName;

            return userName.Equals("root") || userName.Equals("nobody");
        }

        return false;
    }

    public void Save(string name, string value)
    {
        try
        {
            var filePath = Path.Combine(UserSettingsFolder, name);
            File.WriteAllText(filePath, value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    public void Remove(string name)
    {
        try
        {
            var filePath = Path.Combine(UserSettingsFolder, name);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    public string Load(string name)
    {
        try
        {
            var filePath = Path.Combine(UserSettingsFolder, name);
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        return string.Empty;
    }
}
