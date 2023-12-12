using System.Text.Json;
using System.Text.RegularExpressions;

internal static class Utils
{
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

    public static string SanitizeForLog(this string input)
    {
        return Regex.Replace(input, @"\W", "_");
    }

    public static bool IsBase64Image(string data)
    {
        return data.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);
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
