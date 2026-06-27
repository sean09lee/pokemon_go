using System.Globalization;
using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

/// <summary>
/// Service for consuming all documented PoGoAPI endpoints.
/// Base address should be configured as https://pogoapi.net/
/// </summary>
public class PogoApiService(PogoHelpers pogoHelpers)
{
  private readonly PogoHelpers _helper = pogoHelpers;

  public Task<PogoApiResponse> GetApiHashesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("api_hashes.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonNamesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_names.json", cancellationToken);
  public Task<PogoApiResponse> GetReleasedPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("released_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetNestingPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("nesting_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetShinyPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("shiny_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetRaidExclusivePokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("raid_exclusive_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetAlolanPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("alolan_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetPossibleDittoPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("possible_ditto_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonStatsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_stats.json", cancellationToken);
  public async Task<IList<PokemonStatsInfo>> GetPokemonStatsDataAsync(CancellationToken cancellationToken = default)
    {
      var stats = await _helper.GetTypedJsonAsync<IList<PokemonStatsInfo>>("pokemon_stats.json", cancellationToken) ?? [];
      if (stats.Count == 0)
      {
        return stats;
      }

      var filtered = new List<PokemonStatsInfo>();

      foreach (var group in stats
        .Where(x => x.PokemonId.HasValue)
        .GroupBy(x => x.PokemonId!.Value))
      {
        var allowedForms = group.Where(IsAllowedStatForm).ToList();
        if (allowedForms.Count > 0)
        {
          filtered.AddRange(allowedForms);
        }
        else
        {
          filtered.Add(group.First());
        }
      }

      // Keep entries that do not have a Pokémon number, unchanged.
      filtered.AddRange(stats.Where(x => !x.PokemonId.HasValue));

      return filtered;
    }
  public Task<PogoApiResponse> GetFastMovesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("fast_moves.json", cancellationToken);
  public Task<PogoApiResponse> GetChargedMovesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("charged_moves.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonMaxCpAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_max_cp.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonBuddyDistancesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_buddy_distances.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonCandyToEvolveAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_candy_to_evolve.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonEncounterDataAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_encounter_data.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonTypesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_types.json", cancellationToken);
  public Task<PogoApiResponse> GetWeatherBoostsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("weather_boosts.json", cancellationToken);
  public Task<PogoApiResponse> GetTypeEffectivenessAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("type_effectiveness.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonRarityAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_rarity.json", cancellationToken);
    public async Task<IReadOnlyDictionary<decimal, PokemonPowerupRequirement>> GetPokemonPowerupRequirementsAsync(CancellationToken cancellationToken = default)
    {
      var raw = await _helper.GetTypedJsonAsync<Dictionary<string, PokemonPowerupRequirement>>("pokemon_powerup_requirements.json", cancellationToken) ?? [];
      return raw
        .Where(x => decimal.TryParse(x.Key, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        .ToDictionary(
          x => decimal.Parse(x.Key, NumberStyles.Number, CultureInfo.InvariantCulture),
          x => x.Value);
    }
  public Task<PogoApiResponse> GetPokemonGendersAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_genders.json", cancellationToken);
  public Task<PogoApiResponse> GetPlayerXpRequirementsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("player_xp_requirements.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonGenerationsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_generations.json", cancellationToken);
  public Task<PogoApiResponse> GetShadowPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("shadow_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetPokemonFormsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_forms.json", cancellationToken);
  public Task<PogoApiResponse> GetCurrentPokemonMovesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("current_pokemon_moves.json", cancellationToken);
  public Task<PogoApiResponse> GetPvpExclusivePokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pvp_exclusive_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetGalarianPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("galarian_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetCpMultiplierAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("cp_multiplier.json", cancellationToken);
  public Task<PogoApiResponse> GetCommunityDaysAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("community_days.json", cancellationToken);
  public async Task<IList<PokemonEvolution>> GetPokemonEvolutionsAsync(CancellationToken cancellationToken = default)
      => await _helper.GetTypedJsonAsync<IList<PokemonEvolution>>("pokemon_evolutions.json", cancellationToken) ?? [];
    public async Task<IList<PokemonTypeInfo>> GetPokemonTypesDataAsync(CancellationToken cancellationToken = default)
      => await _helper.GetTypedJsonAsync<IList<PokemonTypeInfo>>("pokemon_types.json", cancellationToken) ?? [];
  public Task<PogoApiResponse> GetRaidBossesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("raid_bosses.json", cancellationToken);
  public Task<PogoApiResponse> GetResearchTaskExclusivePokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("research_task_exclusive_pokemon.json", cancellationToken);
  public async Task<IList<MegaPokemon>> GetMegaPokemonAsync(CancellationToken cancellationToken = default)
      => await _helper.GetTypedJsonAsync<IList<MegaPokemon>>("mega_pokemon.json", cancellationToken) ?? [];
  public Task<PogoApiResponse> GetPokemonHeightWeightScaleAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pokemon_height_weight_scale.json", cancellationToken);
  public Task<PogoApiResponse> GetLevelupRewardsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("levelup_rewards.json", cancellationToken);
  public Task<PogoApiResponse> GetBadgesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("badges.json", cancellationToken);
  public Task<PogoApiResponse> GetGoBattleLeagueRewardsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("gobattle_league_rewards.json", cancellationToken);
  public Task<PogoApiResponse> GetRaidSettingsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("raid_settings.json", cancellationToken);
  public Task<PogoApiResponse> GetMegaEvolutionSettingsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("mega_evolution_settings.json", cancellationToken);
  public Task<PogoApiResponse> GetFriendshipLevelSettingsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("friendship_level_settings.json", cancellationToken);
  public Task<PogoApiResponse> GetGoBattleRankingSettingsAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("gobattle_ranking_settings.json", cancellationToken);
  public Task<PogoApiResponse> GetBabyPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("baby_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetPvpFastMovesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pvp_fast_moves.json", cancellationToken);
  public Task<PogoApiResponse> GetPvpChargedMovesAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("pvp_charged_moves.json", cancellationToken);
  public Task<PogoApiResponse> GetTimeLimitedShinyPokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("time_limited_shiny_pokemon.json", cancellationToken);
  public Task<PogoApiResponse> GetPhotobombExclusivePokemonAsync(CancellationToken cancellationToken = default) => _helper.GetJsonAsync("photobomb_exclusive_pokemon.json", cancellationToken);

  private static bool IsAllowedStatForm(PokemonStatsInfo pokemon)
  {
      var form = pokemon.Form?.Trim();
      if (string.IsNullOrWhiteSpace(form))
      {
          return true; // treat blank as Normal
      }

      return form.Equals("normal", StringComparison.OrdinalIgnoreCase)
          || form.Equals("hisuian", StringComparison.OrdinalIgnoreCase)
          || form.Equals("galarian", StringComparison.OrdinalIgnoreCase)
          || form.Equals("alola", StringComparison.OrdinalIgnoreCase)
          || form.Equals("alolan", StringComparison.OrdinalIgnoreCase);
  }
}