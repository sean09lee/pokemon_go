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

        return new RankingResponse
        {
            NormalRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.NormalTierList, evolutionResponse, cancellationToken),
            FireRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FireTierList, evolutionResponse, cancellationToken),
            WaterRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.WaterTierList, evolutionResponse, cancellationToken),
            ElectricRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.ElectricTierList, evolutionResponse, cancellationToken),
            GrassRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.GrassTierList, evolutionResponse, cancellationToken),
            IceRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.IceTierList, evolutionResponse, cancellationToken),
            FightingRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FightingTierList, evolutionResponse, cancellationToken),
            PoisonRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.PoisonTierList, evolutionResponse, cancellationToken),
            GroundRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.GroundTierList, evolutionResponse, cancellationToken),
            FlyingRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FlyingTierList, evolutionResponse, cancellationToken),
            PsychicRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.PsychicTierList, evolutionResponse, cancellationToken),
            BugRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.BugTierList, evolutionResponse, cancellationToken),
            RockRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.RockTierList, evolutionResponse, cancellationToken),
            GhostRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.GhostTierList, evolutionResponse, cancellationToken),
            DragonRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.DragonTierList, evolutionResponse, cancellationToken),
            DarkRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.DarkTierList, evolutionResponse, cancellationToken),
            SteelRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.SteelTierList, evolutionResponse, cancellationToken),
            FairyRaidTeam = await RankingHelper.BuildTopRaidTeamAsync(ownedPokemon, hubRanking.FairyTierList, evolutionResponse, cancellationToken)
        };
    }
}
