using System.Text;

namespace dig.server;

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
    /// If its already hex it should return the original value.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>The string representation of the hexadecimal input.</returns>
    public static string FromHex(this string hex)
    {
        // Remove any leading "0x" or "0X" if present
        hex = hex.TrimStart().ToLowerInvariant();
        if (hex.StartsWith("0x"))
        {
            hex = hex.Substring(2);
        }

        // Convert the hexadecimal string to bytes
        try
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            // Convert bytes to UTF-8 string
            return Encoding.UTF8.GetString(bytes).Trim();
        }
        catch
        {
            // Error occurred during conversion, return the original input
            return hex;
        }
    }

}
