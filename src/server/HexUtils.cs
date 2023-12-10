using System.Text;

/// <summary>
/// Provides utility methods for converting strings to and from hexadecimal representation.
/// </summary>
internal static class HexUtils
{
    /// <summary>
    /// Converts a string to its hexadecimal representation.
    /// </summary>
    /// <param name="input">The input string to convert.</param>
    /// <returns>The hexadecimal representation of the input string.</returns>
    public static string ToHex(this string input) => BitConverter.ToString(Encoding.UTF8.GetBytes(input)).Replace("-", "").ToLowerInvariant();

    /// <summary>
    /// Converts a hexadecimal string to its corresponding string representation.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>The string representation of the hexadecimal input.</returns>
    public static string FromHex(this string hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = hex[2..];
        }

        return Encoding.UTF8.GetString(Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray());
    }
}
