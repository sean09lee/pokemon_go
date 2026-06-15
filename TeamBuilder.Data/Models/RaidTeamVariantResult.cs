namespace TeamBuilder.Data.Models;

public sealed class RaidTeamVariantResult
{
    public IList<RankedPokemon> NoInvestmentTeam { get; init; } = [];
    public IList<RankedPokemon> CurrentResourcesTeam { get; init; } = [];
    public IList<RankedPokemon> UnlimitedResourcesTeam { get; init; } = [];
}
