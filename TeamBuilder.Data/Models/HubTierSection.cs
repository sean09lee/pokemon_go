namespace TeamBuilder.Data.Models;

public sealed class HubTierSection
{
    public required string Label { get; init; }
    public required IList<HubRankedPokemon> TierList { get; init; }
}