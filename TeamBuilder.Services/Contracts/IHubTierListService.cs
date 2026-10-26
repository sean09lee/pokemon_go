using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;

namespace TeamBuilder.Services.Contracts;

/// <summary>
/// Crawls and assembles Pokemon GO Hub tier list data across all attacker types.
/// </summary>
public interface IHubTierListService
{
    /// <summary>
    /// Crawls every attacker type via the Hub API, returning both the assembled
    /// <see cref="HubRankingResponse"/> and the flattened, sorted list of rows.
    /// </summary>
    /// <param name="limitPerType">Maximum number of ranked Pokemon to retrieve per attacker type.</param>
    /// <param name="reportProgressAsync">Optional callback invoked after each attacker type is processed, receiving the type key, the index processed, and the total count.</param>
    /// <param name="cancellationToken">Token used to cancel the crawl.</param>
    Task<(HubRankingResponse Response, List<HubTierRow> Rows)> CrawlAllTypesAsync(
        int limitPerType = 200,
        Func<string, int, int, Task>? reportProgressAsync = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads tier list rows directly from the on-disk JSON cache for every attacker type, without
    /// performing any crawl. Attacker types without a cache file are skipped.
    /// </summary>
    /// <param name="limitPerType">Maximum number of ranked Pokemon to read per attacker type.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<List<HubTierRow>> LoadAllTypesFromCacheAsync(
        int limitPerType = 200,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a <see cref="HubRankingResponse"/> from a previously crawled, flattened set of tier rows
    /// without making any additional Hub API calls. Returns null if no rows are supplied.
    /// </summary>
    HubRankingResponse? BuildFromCachedRows(IReadOnlyCollection<HubTierRow> rows);
}
