using System.Web;
using chia.dotnet;
using dig.caching;

namespace dig;

public class StorePreCacheService(DataLayerProxy dataLayer,
                                    IObjectCache objectCacheService,
                                    ILogger<StorePreCacheService> logger,
                                    IConfiguration configuration)
{
    private readonly DataLayerProxy _dataLayer = dataLayer;
    private readonly IObjectCache _objectCacheService = objectCacheService;
    private readonly ILogger<StorePreCacheService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task CacheStore(string storeId, CancellationToken cancellationToken)
    {
        _objectCacheService.RemoveStore(storeId);

        var rootHash = await _dataLayer.GetRoot(storeId, cancellationToken);
        await _objectCacheService.SetValueAsync(storeId, "", "last-root", rootHash, cancellationToken);

        var storeKeys = await _dataLayer.GetKeys(storeId, rootHash.Hash, cancellationToken) ?? [];
        await _objectCacheService.SetValueAsync(storeId, rootHash.Hash, "keys", storeKeys, cancellationToken);

        foreach (var key in storeKeys)
        {
            _logger.LogInformation("Pre-caching key {key} for storeId {storeId}.", key.SanitizeForLog(), storeId.SanitizeForLog());
            var value = await _dataLayer.GetValue(storeId, key, rootHash.Hash, cancellationToken);
            if (value != null)
            {
                await _objectCacheService.SetValueAsync(storeId, rootHash.Hash, key, value, cancellationToken);
            }

            if (!_configuration.GetValue("dig:DisableProofOfInclusion", true))
            {
                _logger.LogInformation("Pre-caching proof of {key} for storeId {storeId}.", key.SanitizeForLog(), storeId.SanitizeForLog());
                var proof = await _dataLayer.GetProof(storeId, [HttpUtility.UrlDecode(key)], cancellationToken);
                await _objectCacheService.SetValueAsync(storeId, rootHash.Hash, key, proof.ToJson(), cancellationToken);
            }
        }
    }
}
