using System.Text.Json.Serialization;

namespace TeamBuilder.Data.Models;

public sealed class PokemonEvolutionDetail
{
    [JsonPropertyName("buddy_distance_required")]
    public double? BuddyDistanceRequired { get; set; }

    [JsonPropertyName("candy_required")]
    public int? CandyRequired { get; set; }

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("gender_required")]
    public string? GenderRequired { get; set; }

    [JsonPropertyName("item_required")]
    public string? ItemRequired { get; set; }

    [JsonPropertyName("lure_required")]
    public string? LureRequired { get; set; }

    [JsonPropertyName("must_be_buddy_to_evolve")]
    public bool? MustBeBuddyToEvolve { get; set; }

    [JsonPropertyName("no_candy_cost_if_traded")]
    public bool? NoCandyCostIfTraded { get; set; }

    [JsonPropertyName("only_evolves_in_daytime")]
    public bool? OnlyEvolvesInDaytime { get; set; }

    [JsonPropertyName("only_evolves_in_nighttime")]
    public bool? OnlyEvolvesInNighttime { get; set; }

    [JsonPropertyName("pokemon_id")]
    public int? PokemonId { get; set; }

    [JsonPropertyName("pokemon_name")]
    public string? PokemonName { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("upside_down")]
    public bool? UpsideDown { get; set; }
}
