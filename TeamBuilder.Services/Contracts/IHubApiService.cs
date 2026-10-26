using TeamBuilder.Data.Models;

namespace TeamBuilder.Services.Contracts;

/// <summary>
/// This service hits the pokemon go hub api to get tier list data
/// </summary>
public interface IHubApiService
{
    Task<IList<HubRankedPokemon>> CrawlBestPerTypeAsync(string attackerType, int limit = 200, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads previously cached ranking data for the given attacker type without performing any crawl.
    /// Returns null if no cache file exists for the given type/limit combination.
    /// </summary>
    Task<IList<HubRankedPokemon>?> TryGetCachedBestPerTypeAsync(string attackerType, int limit = 200, CancellationToken cancellationToken = default);
}
