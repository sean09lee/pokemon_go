using System.Text.Json;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.AiTools;

namespace TeamBuilder.Tests.AiTools;

public class GetPokemonBaseStatsToolTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsMatchingStats_WhenPokemonFound()
    {
        var pogoApiService = new FakePogoApiService
        {
            StatsToReturn =
            [
                new PokemonStatsInfo { PokemonName = "Swampert", BaseAttack = 208, BaseDefense = 175, BaseStamina = 216 },
                new PokemonStatsInfo { PokemonName = "Charizard", BaseAttack = 223, BaseDefense = 173, BaseStamina = 186 }
            ]
        };
        var tool = new GetPokemonBaseStatsTool(pogoApiService);

        var result = await tool.InvokeAsync("""{"pokemonName":"Swampert"}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        var match = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("Swampert", match.GetProperty("PokemonName").GetString());
        Assert.Equal(208, match.GetProperty("BaseAttack").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_IsCaseInsensitive()
    {
        var pogoApiService = new FakePogoApiService
        {
            StatsToReturn = [new PokemonStatsInfo { PokemonName = "Swampert", BaseAttack = 208 }]
        };
        var tool = new GetPokemonBaseStatsTool(pogoApiService);

        var result = await tool.InvokeAsync("""{"pokemonName":"swampert"}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task InvokeAsync_ReturnsError_WhenPokemonNameMissing()
    {
        var tool = new GetPokemonBaseStatsTool(new FakePogoApiService());

        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task InvokeAsync_ReturnsError_WhenNoMatchFound()
    {
        var pogoApiService = new FakePogoApiService
        {
            StatsToReturn = [new PokemonStatsInfo { PokemonName = "Swampert" }]
        };
        var tool = new GetPokemonBaseStatsTool(pogoApiService);

        var result = await tool.InvokeAsync("""{"pokemonName":"Mewtwo"}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void Definition_HasExpectedFunctionName()
    {
        var tool = new GetPokemonBaseStatsTool(new FakePogoApiService());

        Assert.Equal("get_pokemon_base_stats", tool.Name);
        Assert.Equal("get_pokemon_base_stats", tool.Definition.FunctionName);
    }
}
