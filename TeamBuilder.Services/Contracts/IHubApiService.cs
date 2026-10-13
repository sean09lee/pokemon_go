using TeamBuilder.Data.Models;

namespace TeamBuilder.Services.Contracts;

/// <summary>
/// This service hits the pokemon go hub api to get tier list data
/// </summary>
public interface IHubApiService
{
    Task<IList<HubRankedPokemon>> CrawlBestPerTypeAsync(string attackerType, int limit = 200, CancellationToken cancellationToken = default);
}
