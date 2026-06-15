namespace TeamBuilder.Data.Models;

public sealed class RaidTeamSection
{
    public required string Label { get; init; }
    public required IList<RankedPokemon> Team { get; init; }
}