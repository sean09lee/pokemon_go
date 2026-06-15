using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

public class RankingService(PogoApiService pogoApiService)
{
    private readonly PogoApiService _pogoApiService = pogoApiService;

    public async Task<RankingResponse> BuildRankingResponseAsync(
        IEnumerable<Pokemon> ownedPokemon,
        HubRankingResponse hubRanking,
        int dustLimit,
        IDictionary<int, int> candyByPokemonNumber,
        Func<int, string?, CancellationToken, Task<int>>? resolveCandyAsync = null,
        Func<string, int, Task>? reportProgressAsync = null,
        CancellationToken cancellationToken = default)
    {
        var evolutionResponse = await _pogoApiService.GetPokemonEvolutionsAsync(cancellationToken);
        var megaPokemon = await _pogoApiService.GetMegaPokemonAsync(cancellationToken);
        var pokemonTypes = await _pogoApiService.GetPokemonTypesDataAsync(cancellationToken);
        var powerupRequirements = await _pogoApiService.GetPokemonPowerupRequirementsAsync(cancellationToken);
        var evolutionMap = RankingHelper.BuildEvolutionMap(evolutionResponse, megaPokemon);
        var evolutionCandyCostMap = RankingHelper.BuildEvolutionCandyCostMap(evolutionResponse);
        var tierMap = new Dictionary<string, IList<HubRankedPokemon>>(StringComparer.OrdinalIgnoreCase)
        {
            ["normal"] = hubRanking.NormalTierList,
            ["fire"] = hubRanking.FireTierList,
            ["water"] = hubRanking.WaterTierList,
            ["electric"] = hubRanking.ElectricTierList,
            ["grass"] = hubRanking.GrassTierList,
            ["ice"] = hubRanking.IceTierList,
            ["fighting"] = hubRanking.FightingTierList,
            ["poison"] = hubRanking.PoisonTierList,
            ["ground"] = hubRanking.GroundTierList,
            ["flying"] = hubRanking.FlyingTierList,
            ["psychic"] = hubRanking.PsychicTierList,
            ["bug"] = hubRanking.BugTierList,
            ["rock"] = hubRanking.RockTierList,
            ["ghost"] = hubRanking.GhostTierList,
            ["dragon"] = hubRanking.DragonTierList,
            ["dark"] = hubRanking.DarkTierList,
            ["steel"] = hubRanking.SteelTierList,
            ["fairy"] = hubRanking.FairyTierList
        };

        const int totalTeams = 18;
        var completed = 0;

        async Task ReportAsync(string label)
        {
            if (reportProgressAsync is null)
            {
                return;
            }

            var percent = (int)Math.Round((completed / (double)totalTeams) * 100d);
            await reportProgressAsync($"Building {label} raid team... ({completed}/{totalTeams})", percent);
        }
        var teamsByType = new Dictionary<string, RaidTeamVariantResult>(StringComparer.OrdinalIgnoreCase);
        var globallyReservedOwnedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (attackerType, tierList) in tierMap)
        {
            var label = char.ToUpperInvariant(attackerType[0]) + attackerType[1..];
            await ReportAsync(label);
            teamsByType[attackerType] = await RankingHelper.BuildRaidTeamVariantsAsync(
                ownedPokemon,
                tierList,
                attackerType,
                evolutionMap,
                evolutionCandyCostMap,
                powerupRequirements,
                pokemonTypes,
                megaPokemon,
                dustLimit,
                candyByPokemonNumber,
                resolveCandyAsync,
                globallyReservedOwnedKeys,
                cancellationToken);
            completed++;
        }

        if (reportProgressAsync is not null)
        {
            await reportProgressAsync("Completed raid-team generation.", 100);
        }

        return new RankingResponse
        {
            RaidTeamsByType = teamsByType
        };
    }
}
