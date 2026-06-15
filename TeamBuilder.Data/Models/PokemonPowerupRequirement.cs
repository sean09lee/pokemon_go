using System.Text.Json.Serialization;

namespace TeamBuilder.Data.Models;

public sealed class PokemonPowerupRequirement
{
    [JsonPropertyName("candy_to_upgrade")]
    public int? CandyToUpgrade { get; set; }

    [JsonPropertyName("current_level")]
    public decimal? CurrentLevel { get; set; }

    [JsonPropertyName("level_after_powering")]
    public decimal? LevelAfterPowering { get; set; }

    [JsonPropertyName("stardust_to_upgrade")]
    public int? StardustToUpgrade { get; set; }

    [JsonPropertyName("xl_candy_to_upgrade")]
    public int? XlCandyToUpgrade { get; set; }
}