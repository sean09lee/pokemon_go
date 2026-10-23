using System.Text.Json;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.AiTools;

namespace TeamBuilder.Tests.AiTools;

public class GetPokemonTypesToolTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsMatchingTypes_WhenPokemonFound()
    {
        var pogoApiService = new FakePogoApiService
        {
            TypesToReturn =
            [
                new PokemonTypeInfo { PokemonName = "Charizard", Type = ["fire", "flying"] }
            ]
        };
        var tool = new GetPokemonTypesTool(pogoApiService);

        var result = await tool.InvokeAsync("""{"pokemonName":"Charizard"}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        var match = Assert.Single(document.RootElement.EnumerateArray());
        var types = match.GetProperty("Type").EnumerateArray().Select(t => t.GetString()).ToList();
        Assert.Equal(["fire", "flying"], types);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsError_WhenPokemonNameMissing()
    {
        var tool = new GetPokemonTypesTool(new FakePogoApiService());

        var result = await tool.InvokeAsync("""{"limit":5}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task InvokeAsync_ReturnsError_WhenNoMatchFound()
    {
        var pogoApiService = new FakePogoApiService
        {
            TypesToReturn = [new PokemonTypeInfo { PokemonName = "Charizard", Type = ["fire"] }]
        };
        var tool = new GetPokemonTypesTool(pogoApiService);

        var result = await tool.InvokeAsync("""{"pokemonName":"Blastoise"}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void Definition_HasExpectedFunctionName()
    {
        var tool = new GetPokemonTypesTool(new FakePogoApiService());

        Assert.Equal("get_pokemon_types", tool.Name);
        Assert.Equal("get_pokemon_types", tool.Definition.FunctionName);
    }
}
