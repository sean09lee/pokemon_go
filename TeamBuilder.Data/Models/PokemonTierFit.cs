namespace TeamBuilder.Data.Models;

public sealed record PokemonTierFit
{
    public Pokemon? Pokemon { get; init; }
    public string MatchStatus { get; init; } = "Unknown";
    public HubCounterEntry? Entry { get; init; }
}
