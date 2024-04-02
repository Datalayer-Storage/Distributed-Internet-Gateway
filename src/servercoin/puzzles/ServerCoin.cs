using System.Numerics;

namespace dig.servercoin;

public record ServerCoin
{
    public string LauncherId { get; init; } = string.Empty;
    public string CoinId { get; init; } = string.Empty;
    public BigInteger Amount { get; init; }
    public bool Ours { get; init; }
    public IEnumerable<string> Urls { get; init; } = [];
}
