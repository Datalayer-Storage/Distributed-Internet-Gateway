using chia.dotnet.clvm;
using System.Reflection;

namespace dig.servercoin;

internal static class Puzzles
{
    public static Program LoadPuzzle(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = Path.Combine($"servercoin.puzzles.{name}.clsp.hex");

        using var stream = assembly.GetManifestResourceStream(resourcePath) ?? throw new InvalidOperationException($"Could not find resource: {resourcePath}");
        using var reader = new StreamReader(stream);
        var fileContent = reader.ReadToEnd().Trim();

        return Program.DeserializeHex(fileContent);
    }
}
