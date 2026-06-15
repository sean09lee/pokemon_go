namespace TeamBuilder.Data.Models;

public sealed class HubTierRow
{
    public required string AttackerType { get; init; }
    public required HubRankedPokemon Pokemon { get; init; }
}