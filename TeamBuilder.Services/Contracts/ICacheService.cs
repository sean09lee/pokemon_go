namespace TeamBuilder.Services.Contracts;

/// <summary>
/// Simple JSON-backed disk cache used to reduce redundant calls to external APIs.
/// </summary>
public interface ICacheService
{
    Task<T?> TryGetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);

    Task SaveAsync<T>(string cacheKey, T data, CancellationToken cancellationToken = default);
}
