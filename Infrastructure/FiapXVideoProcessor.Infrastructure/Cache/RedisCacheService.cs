using Microsoft.Extensions.Caching.Distributed;
using VideoProcessor.Application.Interfaces;

namespace VideoProcessor.Infrastructure.Cache;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await _cache.GetStringAsync(key, cancellationToken);
        return value is not null;
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions();
        if (expiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = expiration;

        await _cache.SetStringAsync(key, value, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }
}