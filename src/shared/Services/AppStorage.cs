namespace dig;
internal class AppStorage(string folderName)
{
    private readonly string _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), folderName);

    public void Save(string name, string value)
    {
        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }
        var filePath = Path.Combine(_folderPath, name);
        File.WriteAllText(filePath, value);
    }

    public void Remove(string name)
    {
        var filePath = Path.Combine(_folderPath, name);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    
    public string Load(string name)
    {
        var filePath = Path.Combine(_folderPath, name);
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        return File.ReadAllText(filePath);
    }
}
