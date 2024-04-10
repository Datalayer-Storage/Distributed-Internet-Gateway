using System.Numerics;

namespace dig.servercoin;

/// <summary>
/// Represents a server coin entity.
/// </summary>
public record ServerCoin
{
    /// <summary>
    /// Gets the mojo reserve amount of the server coin.
    /// </summary>
    public BigInteger Amount { get; init; }

    /// <summary>
    /// Gets the coin ID of the server coin.
    /// </summary>
    public string CoinId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the fee of the server coin.
    /// </summary>
    public string LauncherId { get; init; } = string.Empty;

    /// <summary>
    /// Is this our server coin?
    /// </summary>
    public bool Ours { get; init; }

    /// <summary>
    /// The list of server URLs of the server coin.
    /// </summary>
    public IEnumerable<string> Urls { get; init; } = [];
}
