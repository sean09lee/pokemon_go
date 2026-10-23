using System.Text.Json;
using OpenAI.Chat;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.AiTools;

/// <summary>
/// Lets the AI Advisor look up a Pokemon's base attack/defense/stamina stats from the
/// PoGoAPI data instead of guessing from training data.
/// </summary>
public class GetPokemonBaseStatsTool(IPogoApiService pogoApiService) : IAiTool
{
    public string Name => "get_pokemon_base_stats";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_pokemon_base_stats",
        functionDescription: "Gets the base attack, defense, and stamina stats for a Pokemon by name.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "pokemonName": {
                    "type": "string",
                    "description": "The Pokemon species name, e.g. 'Dragonite' or 'Swampert'."
                }
            },
            "required": ["pokemonName"]
        }
        """));

    public async Task<string> InvokeAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var root = document.RootElement;

        var pokemonName = root.TryGetProperty("pokemonName", out var nameProp) ? nameProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(pokemonName))
        {
            return JsonSerializer.Serialize(new { error = "pokemonName is required." });
        }

        var stats = await pogoApiService.GetPokemonStatsDataAsync(cancellationToken);

        var matches = stats
            .Where(s => string.Equals(s.PokemonName, pokemonName, StringComparison.OrdinalIgnoreCase))
            .Select(s => new { s.PokemonName, s.Form, s.BaseAttack, s.BaseDefense, s.BaseStamina })
            .ToList();

        if (matches.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = $"No Pokemon found matching '{pokemonName}'." });
        }

        return JsonSerializer.Serialize(matches);
    }
}
