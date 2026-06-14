using TeamBuilder.Data.Models;

namespace TeamBuilder.Data.Responses;

public class HubRankingResponse
{
    public IList<HubRankedPokemon> NormalTierList { get; init; } = [];
    public IList<HubRankedPokemon> FireTierList { get; init; } = [];
    public IList<HubRankedPokemon> WaterTierList { get; init; } = [];
    public IList<HubRankedPokemon> ElectricTierList { get; init; } = [];
    public IList<HubRankedPokemon> GrassTierList { get; init; } = [];
    public IList<HubRankedPokemon> IceTierList { get; init; } = [];
    public IList<HubRankedPokemon> FightingTierList { get; init; } = [];
    public IList<HubRankedPokemon> PoisonTierList { get; init; } = [];
    public IList<HubRankedPokemon> GroundTierList { get; init; } = [];
    public IList<HubRankedPokemon> FlyingTierList { get; init; } = [];
    public IList<HubRankedPokemon> PsychicTierList { get; init; } = [];
    public IList<HubRankedPokemon> BugTierList { get; init; } = [];
    public IList<HubRankedPokemon> RockTierList { get; init; } = [];
    public IList<HubRankedPokemon> GhostTierList { get; init; } = [];
    public IList<HubRankedPokemon> DragonTierList { get; init; } = [];
    public IList<HubRankedPokemon> DarkTierList { get; init; } = [];
    public IList<HubRankedPokemon> SteelTierList { get; init; } = [];
    public IList<HubRankedPokemon> FairyTierList { get; init; } = [];
}
