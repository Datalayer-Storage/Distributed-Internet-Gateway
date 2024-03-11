using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Dynamic;
using System.Security.Cryptography;

namespace dig;

internal static partial class Utils
{
    public static string MD5Hash(this string input)
    {
        // Convert the input string to a byte array and compute the hash.
        var data = MD5.HashData(Encoding.Default.GetBytes(input));
        return BitConverter.ToString(data).Replace("-", string.Empty);
    }

    public static Exception GetInnermostException(Exception ex)
    {
        if (ex.InnerException == null)
        {
            return ex;
        }

        return GetInnermostException(ex.InnerException);
    }

    public static string GetInnermostExceptionMessage(this Exception ex)
    {
        var innerMost = GetInnermostException(ex);
        return innerMost.Message;
    }

    public static string SanitizePath(this string path, string baseDirectory)
    {
        var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, path));

        if (!fullPath.StartsWith(baseDirectory, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        return fullPath;
    }

    public static string SanitizeForLog(this string input) => SanitizeRegex().Replace(input, "_");

    public static bool IsBase64Image(string data) => data.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);

    public static bool TryParseJson(string strInput, out dynamic? v)
    {
        v = null;

        try
        {
            strInput = strInput.Trim();
            if (strInput.StartsWith('{') && strInput.EndsWith('}') || //For object
                strInput.StartsWith('[') && strInput.EndsWith(']')) //For array
            {
                v = JsonSerializer.Deserialize<ExpandoObject>(strInput);
            }
        }
        catch
        {
        }

        return v is not null;
    }

    [GeneratedRegex(@"\W")] // matches any non-word character
    private static partial Regex SanitizeRegex();
}
