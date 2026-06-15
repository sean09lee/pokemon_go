using System.Text.Json.Serialization;

namespace TeamBuilder.Data.Models;

public sealed class PokemonTypeInfo
{
    [JsonPropertyName("pokemon_id")]
    public int? PokemonId { get; set; }

    [JsonPropertyName("pokemon_name")]
    public string? PokemonName { get; set; }

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("type")]
    public IList<string> Type { get; set; } = [];
}
