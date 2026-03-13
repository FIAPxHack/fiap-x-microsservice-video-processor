namespace VideoProcessor.Application.Interfaces;

public interface ICacheService
{
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}