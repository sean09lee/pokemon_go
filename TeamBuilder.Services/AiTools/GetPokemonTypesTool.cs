using System.Text.Json;
using OpenAI.Chat;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.AiTools;

/// <summary>
/// Lets the AI Advisor look up a Pokemon's elemental type(s) from the PoGoAPI data, grounding
/// type-matchup reasoning in real data instead of the model's general knowledge.
/// </summary>
public class GetPokemonTypesTool(IPogoApiService pogoApiService) : IAiTool
{
    public string Name => "get_pokemon_types";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_pokemon_types",
        functionDescription: "Gets the elemental type(s) of a Pokemon by name, useful for reasoning about type matchups and weaknesses.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "pokemonName": {
                    "type": "string",
                    "description": "The Pokemon species name, e.g. 'Charizard'."
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

        var typeData = await pogoApiService.GetPokemonTypesDataAsync(cancellationToken);

        var matches = typeData
            .Where(t => string.Equals(t.PokemonName, pokemonName, StringComparison.OrdinalIgnoreCase))
            .Select(t => new { t.PokemonName, t.Form, t.Type })
            .ToList();

        if (matches.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = $"No Pokemon found matching '{pokemonName}'." });
        }

        return JsonSerializer.Serialize(matches);
    }
}
