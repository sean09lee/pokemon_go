using System.Text.Json.Serialization;

namespace TeamBuilder.Data.Models;

public sealed class MegaPokemon
{
    [JsonPropertyName("first_time_mega_energy_required")]
    public int? FirstTimeMegaEnergyRequired { get; set; }

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("mega_energy_required")]
    public int? MegaEnergyRequired { get; set; }

    [JsonPropertyName("mega_name")]
    public string? MegaName { get; set; }

    [JsonPropertyName("pokemon_id")]
    public int? PokemonId { get; set; }

    [JsonPropertyName("pokemon_name")]
    public string? PokemonName { get; set; }

    [JsonPropertyName("stats")]
    public MegaPokemonStats? Stats { get; set; }

    [JsonPropertyName("type")]
    public IList<string> Type { get; set; } = [];
}
