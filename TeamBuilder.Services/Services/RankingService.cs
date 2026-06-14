using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

public class RankingService(PogoApiService pogoApiService)
{
    private readonly PogoApiService _pogoApiService = pogoApiService;

    public async Task<RankingResponse> BuildRankingResponseAsync(IEnumerable<Pokemon> ownedPokemon, HubRankingResponse hubRanking, CancellationToken cancellationToken = default)
    {
        var evolutionResponse = await _pogoApiService.GetPokemonEvolutionsAsync(cancellationToken);
        var megaPokemon = await _pogoApiService.GetMegaPokemonAsync(cancellationToken);
        var evolutionMap = RankingHelper.BuildEvolutionMap(evolutionResponse, megaPokemon);

        return new RankingResponse
        {
            NormalRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.NormalTierList, evolutionMap, cancellationToken),
            FireRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FireTierList, evolutionMap, cancellationToken),
            WaterRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.WaterTierList, evolutionMap, cancellationToken),
            ElectricRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.ElectricTierList, evolutionMap, cancellationToken),
            GrassRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.GrassTierList, evolutionMap, cancellationToken),
            IceRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.IceTierList, evolutionMap, cancellationToken),
            FightingRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FightingTierList, evolutionMap, cancellationToken),
            PoisonRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.PoisonTierList, evolutionMap, cancellationToken),
            GroundRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.GroundTierList, evolutionMap, cancellationToken),
            FlyingRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FlyingTierList, evolutionMap, cancellationToken),
            PsychicRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.PsychicTierList, evolutionMap, cancellationToken),
            BugRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.BugTierList, evolutionMap, cancellationToken),
            RockRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.RockTierList, evolutionMap, cancellationToken),
            GhostRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.GhostTierList, evolutionMap, cancellationToken),
            DragonRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.DragonTierList, evolutionMap, cancellationToken),
            DarkRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.DarkTierList, evolutionMap, cancellationToken),
            SteelRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.SteelTierList, evolutionMap, cancellationToken),
            FairyRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FairyTierList, evolutionMap, cancellationToken)
        };
    }
}
