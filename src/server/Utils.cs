using System.Text.Json;
using System.Text.RegularExpressions;

internal static class Utils
{
    public static string SanitizeForLog(this string input)
    {
        return Regex.Replace(input, @"\W", "_");
    }

    public static bool IsBase64Image(string data)
    {
        return data.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetMimeType(string ext)
    {
        if (dig.MimeTypes.TryGetMimeType(ext, out var mimeType))
        {
            return mimeType;
        }

        return null;
    }
    public static bool TryParseJson(string strInput, out dynamic? v)
    {
        try
        {
            strInput = strInput.Trim();
            if (strInput.StartsWith('{') && strInput.EndsWith('}') || //For object
                strInput.StartsWith('[') && strInput.EndsWith(']')) //For array
            {
                v = JsonSerializer.Deserialize<dynamic>(strInput) ?? throw new Exception("Couldn't deserialize JSON");

                return true;
            }
        }
        catch (Exception)
        {
        }

        v = null;
        return false;
    }
}
