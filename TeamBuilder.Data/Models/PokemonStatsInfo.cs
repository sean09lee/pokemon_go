using System.Text.Json.Serialization;

namespace TeamBuilder.Data.Models;

public sealed class PokemonStatsInfo
{
    [JsonPropertyName("pokemon_id")]
    public int? PokemonId { get; set; }

    [JsonPropertyName("pokemon_name")]
    public string? PokemonName { get; set; }

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("base_attack")]
    public int? BaseAttack { get; set; }

    [JsonPropertyName("base_defense")]
    public int? BaseDefense { get; set; }

    [JsonPropertyName("base_stamina")]
    public int? BaseStamina { get; set; }
}
