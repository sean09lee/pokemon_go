using System.Text.Json.Serialization;

namespace TeamBuilder.Data.Models;

public sealed class PokemonEvolution
{
    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("pokemon_id")]
    public int? PokemonId { get; set; }

    [JsonPropertyName("pokemon_name")]
    public string? PokemonName { get; set; }

    [JsonPropertyName("evolutions")]
    public IList<PokemonEvolutionDetail> Evolutions { get; set; } = [];
}
