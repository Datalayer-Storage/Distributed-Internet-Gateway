using System.Text.RegularExpressions;

namespace dig.servercoin;

internal static partial class HexHelper
{
    public static string SanitizeHex(this string hex) => hex.Replace("0x", string.Empty).Replace("0X", string.Empty);

    public static string FormatHex(this string hex) => MyRegex().IsMatch(hex) ? hex : $"0x{hex}";

    [GeneratedRegex("^0x", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex MyRegex();
}
