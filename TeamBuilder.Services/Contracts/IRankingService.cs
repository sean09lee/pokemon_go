using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;

namespace TeamBuilder.Services.Contracts;

public interface IRankingService
{
    Task<RankingResponse> BuildRankingResponseAsync(
        IEnumerable<Pokemon> ownedPokemon,
        HubRankingResponse hubRanking,
        int dustLimit,
        IDictionary<int, int> candyByPokemonNumber,
        Func<int, string?, CancellationToken, Task<int>>? resolveCandyAsync = null,
        Func<string, int, Task>? reportProgressAsync = null,
        CancellationToken cancellationToken = default);
}
