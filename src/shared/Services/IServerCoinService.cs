using dig.servercoin;

namespace dig;

/// <summary>
/// Interface for server coin service.
/// </summary>
public interface IServerCoinService
{
    /// <summary>
    /// Creates a server coin.
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="serverUrl"></param>
    /// <param name="mojoReserveAmount"></param>
    /// <param name="fee"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ServerCoin> CreateCoin(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee, CancellationToken cancellationToken);

    Task<bool> AddServer(string storeId, string serverUrl, ulong mojoReserveAmount, ulong fee, CancellationToken cancellationToken);

    /// <summary>
    /// Spends a server coin, effectively removing it from the store.
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="coinId"></param>
    /// <param name="fee"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> SpendCoin(string storeId, string coinId, ulong fee, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all server coins for a store.
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <remarks>If the implementing service caches the coins locally, it should return the cached coins.</remarks>
    Task<IEnumerable<ServerCoin>> GetCoins(string storeId, CancellationToken cancellationToken);

    /// <summary>
    /// Syncs all server coins for a store, getting retrieving them from the blockchain if necessary.
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<ServerCoin>> SyncCoins(string storeId, CancellationToken cancellationToken);
}
