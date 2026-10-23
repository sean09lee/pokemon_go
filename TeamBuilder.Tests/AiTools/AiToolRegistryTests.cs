using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.AiTools;

namespace TeamBuilder.Tests.AiTools;

public class AiToolRegistryTests
{
    [Fact]
    public void GetToolDefinitions_ReturnsOneDefinitionPerRegisteredTool()
    {
        var registry = new AiToolRegistry([
            new GetPokemonBaseStatsTool(new FakePogoApiService()),
            new GetPokemonTypesTool(new FakePogoApiService())
        ], NullLogger<AiToolRegistry>.Instance);

        var definitions = registry.GetToolDefinitions();

        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, d => d.FunctionName == "get_pokemon_base_stats");
        Assert.Contains(definitions, d => d.FunctionName == "get_pokemon_types");
    }

    [Fact]
    public async Task ExecuteAsync_DispatchesToMatchingToolByName()
    {
        var pogoApiService = new FakePogoApiService
        {
            StatsToReturn = [new PokemonStatsInfo { PokemonName = "Swampert", BaseAttack = 208 }]
        };
        var registry = new AiToolRegistry([new GetPokemonBaseStatsTool(pogoApiService)], NullLogger<AiToolRegistry>.Instance);

        var result = await registry.ExecuteAsync("get_pokemon_base_stats", """{"pokemonName":"Swampert"}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenToolNameUnknown()
    {
        var registry = new AiToolRegistry([new GetPokemonBaseStatsTool(new FakePogoApiService())], NullLogger<AiToolRegistry>.Instance);

        var result = await registry.ExecuteAsync("not_a_real_tool", "{}", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("not_a_real_tool", error.GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenToolThrows()
    {
        var registry = new AiToolRegistry([new ThrowingTool()], NullLogger<AiToolRegistry>.Instance);

        var result = await registry.ExecuteAsync("throwing_tool", "{}", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("error", out var error));
        Assert.Equal("boom", error.GetString());
    }

    private sealed class ThrowingTool : IAiTool
    {
        public string Name => "throwing_tool";

        public OpenAI.Chat.ChatTool Definition { get; } = OpenAI.Chat.ChatTool.CreateFunctionTool(
            functionName: "throwing_tool",
            functionDescription: "A tool that always throws, used to test registry error handling.");

        public Task<string> InvokeAsync(string argumentsJson, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
