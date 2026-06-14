using System.Text.Json.Serialization;

namespace TeamBuilder.Data.Models;

public sealed class MegaPokemonStats
{
    [JsonPropertyName("base_attack")]
    public int? BaseAttack { get; set; }

    [JsonPropertyName("base_defense")]
    public int? BaseDefense { get; set; }

    [JsonPropertyName("base_stamina")]
    public int? BaseStamina { get; set; }
}
