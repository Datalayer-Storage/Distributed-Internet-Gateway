namespace dig;
internal class AppStorage
{
    private readonly string _folderPath = string.Empty;

    public AppStorage(string folderName)
    {
        try
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), folderName);
            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }

            string sourceFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(sourceFileName))
            {
                // create the user settings file if it doesn't exit already
                File.Copy(sourceFileName, UserSettingsFilePath, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    public string UserSettingsFilePath => Path.Combine(_folderPath, "appsettings.user.json");
    public string UserSettingsFolder => _folderPath;

    public void Save(string name, string value)
    {
        try
        {
            var filePath = Path.Combine(_folderPath, name);
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
            var filePath = Path.Combine(_folderPath, name);
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
            var filePath = Path.Combine(_folderPath, name);
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
