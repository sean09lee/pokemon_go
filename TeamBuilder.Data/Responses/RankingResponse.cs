using TeamBuilder.Data.Models;

namespace TeamBuilder.Data.Responses;

public class RankingResponse
{
    public IList<RankedPokemon> NormalRaidTeam { get; init; } = [];
    public IList<RankedPokemon> FireRaidTeam { get; init; } = [];
    public IList<RankedPokemon> WaterRaidTeam { get; init; } = [];
    public IList<RankedPokemon> ElectricRaidTeam { get; init; } = [];
    public IList<RankedPokemon> GrassRaidTeam { get; init; } = [];
    public IList<RankedPokemon> IceRaidTeam { get; init; } = [];
    public IList<RankedPokemon> FightingRaidTeam { get; init; } = [];
    public IList<RankedPokemon> PoisonRaidTeam { get; init; } = [];
    public IList<RankedPokemon> GroundRaidTeam { get; init; } = [];
    public IList<RankedPokemon> FlyingRaidTeam { get; init; } = [];
    public IList<RankedPokemon> PsychicRaidTeam { get; init; } = [];
    public IList<RankedPokemon> BugRaidTeam { get; init; } = [];
    public IList<RankedPokemon> RockRaidTeam { get; init; } = [];
    public IList<RankedPokemon> GhostRaidTeam { get; init; } = [];
    public IList<RankedPokemon> DragonRaidTeam { get; init; } = [];
    public IList<RankedPokemon> DarkRaidTeam { get; init; } = [];
    public IList<RankedPokemon> SteelRaidTeam { get; init; } = [];
    public IList<RankedPokemon> FairyRaidTeam { get; init; } = [];
    public IList<RankedPokemon> GreatLeague { get; init; } = [];
    public IList<RankedPokemon> UltraLeague { get; init; } = [];
    public IList<RankedPokemon> MasterLeague { get; init; } = [];
}
