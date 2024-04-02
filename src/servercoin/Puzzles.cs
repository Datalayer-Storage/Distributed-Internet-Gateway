using chia.dotnet.clvm;
using System.Reflection;

namespace dig.servercoin;

internal static class Puzzles
{
    public static Program LoadPuzzle(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = "servercoin.puzzles";

        resourcePath = Path.Combine(resourcePath, $"{name}.clsp.hex");
        resourcePath = resourcePath.Replace(Path.DirectorySeparatorChar, '.');

        using Stream stream = assembly.GetManifestResourceStream(resourcePath) ?? throw new InvalidOperationException($"Could not find resource: {resourcePath}");
        using StreamReader reader = new(stream);
        var fileContent = reader.ReadToEnd().Trim();

        return Program.DeserializeHex(fileContent);
    }
}
