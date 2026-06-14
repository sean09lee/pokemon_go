namespace TeamBuilder.Data.Models;

public sealed record HubCounterEntry
{
    public int? PokemonNumber { get; init; }
    public string? Name { get; init; }
    public string? Form { get; init; }
    public bool? IsShadow { get; init; }
    public int? Rank { get; init; }
    public string? Tier { get; init; }
    public decimal? Score { get; init; }
    public string VariantKey { get; init; } = "normal";
}
