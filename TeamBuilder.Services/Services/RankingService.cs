using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

public class RankingService
{
    public RankingResponse BuildRankingResponse(IEnumerable<Pokemon> ownedPokemon, HubRankingResponse hubRanking)
    {
        return new RankingResponse
        {
            NormalRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.NormalTierList),
            FireRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.FireTierList),
            WaterRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.WaterTierList),
            ElectricRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.ElectricTierList),
            GrassRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.GrassTierList),
            IceRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.IceTierList),
            FightingRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.FightingTierList),
            PoisonRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.PoisonTierList),
            GroundRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.GroundTierList),
            FlyingRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.FlyingTierList),
            PsychicRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.PsychicTierList),
            BugRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.BugTierList),
            RockRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.RockTierList),
            GhostRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.GhostTierList),
            DragonRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.DragonTierList),
            DarkRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.DarkTierList),
            SteelRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.SteelTierList),
            FairyRaidTeam = RankingHelper.BuildTopRaidTeam(ownedPokemon, hubRanking.FairyTierList)
        };
    }

    public Task<RankingResponse> GetRankingAsync()
    {
        throw new NotImplementedException();
    }
}
