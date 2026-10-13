using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;

namespace TeamBuilder.Services.Contracts;

/// <summary>
/// Service for consuming all documented PoGoAPI endpoints.
/// Base address should be configured as https://pogoapi.net/
/// </summary>
public interface IPogoApiService
{
    Task<PogoApiResponse> GetApiHashesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonNamesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetReleasedPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetNestingPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetShinyPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetRaidExclusivePokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetAlolanPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPossibleDittoPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonStatsAsync(CancellationToken cancellationToken = default);
    Task<IList<PokemonStatsInfo>> GetPokemonStatsDataAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetFastMovesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetChargedMovesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonMaxCpAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonBuddyDistancesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonCandyToEvolveAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonEncounterDataAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonTypesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetWeatherBoostsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetTypeEffectivenessAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonRarityAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<decimal, PokemonPowerupRequirement>> GetPokemonPowerupRequirementsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonGendersAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPlayerXpRequirementsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonGenerationsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetShadowPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonFormsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetCurrentPokemonMovesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPvpExclusivePokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetGalarianPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetCpMultiplierAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetCommunityDaysAsync(CancellationToken cancellationToken = default);
    Task<IList<PokemonEvolution>> GetPokemonEvolutionsAsync(CancellationToken cancellationToken = default);
    Task<IList<PokemonTypeInfo>> GetPokemonTypesDataAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetRaidBossesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetResearchTaskExclusivePokemonAsync(CancellationToken cancellationToken = default);
    Task<IList<MegaPokemon>> GetMegaPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPokemonHeightWeightScaleAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetLevelupRewardsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetBadgesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetGoBattleLeagueRewardsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetRaidSettingsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetMegaEvolutionSettingsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetFriendshipLevelSettingsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetGoBattleRankingSettingsAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetBabyPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPvpFastMovesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPvpChargedMovesAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetTimeLimitedShinyPokemonAsync(CancellationToken cancellationToken = default);
    Task<PogoApiResponse> GetPhotobombExclusivePokemonAsync(CancellationToken cancellationToken = default);
}
