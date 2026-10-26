using System.Text.Json;
using OpenAI.Chat;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.AiTools;

/// <summary>
/// Lets the AI Advisor look up the top ranked Pokemon GO attackers for a given move type,
/// grounded in the live Pokemon GO Hub tier list instead of the model's training data.
/// </summary>
public class GetBestPokemonByTypeTool(IHubApiService hubApiService) : IAiTool
{
    public string Name => "get_best_pokemon_by_type";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_best_pokemon_by_type",
        functionDescription: "Gets the top ranked Pokemon GO attackers for a given attacker type (e.g. 'fire', 'water', 'dragon'), ranked by DPS/TDO from the Pokemon GO Hub tier list.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "attackerType": {
                    "type": "string",
                    "description": "The Pokemon move/attacker type to rank, e.g. 'fire', 'water', 'dragon'."
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of ranked Pokemon to return. Defaults to 10, max 25."
                }
            },
            "required": ["attackerType"]
        }
        """));

    public async Task<string> InvokeAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var root = document.RootElement;

        var attackerType = root.TryGetProperty("attackerType", out var typeProp) ? typeProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(attackerType))
        {
            return JsonSerializer.Serialize(new { error = "attackerType is required." });
        }

        var limit = root.TryGetProperty("limit", out var limitProp) && limitProp.TryGetInt32(out var parsedLimit)
            ? Math.Clamp(parsedLimit, 1, 25)
            : 10;

        var ranked = await hubApiService.CrawlBestPerTypeAsync(attackerType, Math.Max(limit, 25), cancellationToken: cancellationToken);

        var top = ranked
            .Take(limit)
            .Select(p => new
            {
                p.Rank,
                p.Name,
                p.Form,
                p.FastAttack,
                p.ChargedAttack,
                p.DPS,
                p.TDO
            });

        return JsonSerializer.Serialize(top);
    }
}
