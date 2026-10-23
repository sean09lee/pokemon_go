using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Tests.AiTools;

/// <summary>
/// Minimal <see cref="IPogoApiService"/> test double that only implements the members exercised
/// by the AI tool tests. All other members throw so an accidental dependency is caught quickly.
/// </summary>
internal sealed class FakePogoApiService : IPogoApiService
{
    public IList<PokemonStatsInfo> StatsToReturn { get; set; } = [];
    public IList<PokemonTypeInfo> TypesToReturn { get; set; } = [];

    public Task<IList<PokemonStatsInfo>> GetPokemonStatsDataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(StatsToReturn);

    public Task<IList<PokemonTypeInfo>> GetPokemonTypesDataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TypesToReturn);

    public Task<PogoApiResponse> GetApiHashesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonNamesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetReleasedPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetNestingPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetShinyPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetRaidExclusivePokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetAlolanPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPossibleDittoPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonStatsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetFastMovesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetChargedMovesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonMaxCpAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonBuddyDistancesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonCandyToEvolveAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonEncounterDataAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonTypesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetWeatherBoostsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetTypeEffectivenessAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonRarityAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyDictionary<decimal, PokemonPowerupRequirement>> GetPokemonPowerupRequirementsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonGendersAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPlayerXpRequirementsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonGenerationsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetShadowPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonFormsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetCurrentPokemonMovesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPvpExclusivePokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetGalarianPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetCpMultiplierAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetCommunityDaysAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IList<PokemonEvolution>> GetPokemonEvolutionsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetRaidBossesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetResearchTaskExclusivePokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IList<MegaPokemon>> GetMegaPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPokemonHeightWeightScaleAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetLevelupRewardsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetBadgesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetGoBattleLeagueRewardsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetRaidSettingsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetMegaEvolutionSettingsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetFriendshipLevelSettingsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetGoBattleRankingSettingsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetBabyPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPvpFastMovesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPvpChargedMovesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetTimeLimitedShinyPokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PogoApiResponse> GetPhotobombExclusivePokemonAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

/// <summary>
/// Minimal <see cref="IHubApiService"/> test double used by the AI tool tests.
/// </summary>
internal sealed class FakeHubApiService : IHubApiService
{
    public IList<HubRankedPokemon> RankedPokemonToReturn { get; set; } = [];
    public string? LastRequestedAttackerType { get; private set; }
    public int LastRequestedLimit { get; private set; }

    public Task<IList<HubRankedPokemon>> CrawlBestPerTypeAsync(string attackerType, int limit = 200, CancellationToken cancellationToken = default)
    {
        LastRequestedAttackerType = attackerType;
        LastRequestedLimit = limit;
        return Task.FromResult(RankedPokemonToReturn);
    }
}
