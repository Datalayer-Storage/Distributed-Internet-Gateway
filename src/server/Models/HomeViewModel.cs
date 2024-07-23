using System.Numerics;

public class HomeViewModel
{
    public IEnumerable<Store> Stores { get; init; } = [];
    public BigInteger WalletBalance { get; init; }
    public string NodeAddress { get; init; } = string.Empty;
}
