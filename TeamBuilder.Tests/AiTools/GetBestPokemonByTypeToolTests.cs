using System.Text.Json;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.AiTools;

namespace TeamBuilder.Tests.AiTools;

public class GetBestPokemonByTypeToolTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsRankedPokemon_WhenAttackerTypeProvided()
    {
        var hubApiService = new FakeHubApiService
        {
            RankedPokemonToReturn =
            [
                new HubRankedPokemon { Rank = 1, Name = "Reshiram", FastAttack = "Fire Fang", ChargedAttack = "Overheat", DPS = 20.1, TDO = 150.2 },
                new HubRankedPokemon { Rank = 2, Name = "Charizard", FastAttack = "Fire Spin", ChargedAttack = "Blast Burn", DPS = 18.4, TDO = 140.0 }
            ]
        };
        var tool = new GetBestPokemonByTypeTool(hubApiService);

        var result = await tool.InvokeAsync("""{"attackerType":"fire","limit":1}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        var entries = document.RootElement.EnumerateArray().ToList();
        Assert.Single(entries);
        Assert.Equal("Reshiram", entries[0].GetProperty("Name").GetString());
        Assert.Equal("fire", hubApiService.LastRequestedAttackerType);
    }

    [Fact]
    public async Task InvokeAsync_DefaultsLimitToTen_WhenNotSpecified()
    {
        var hubApiService = new FakeHubApiService
        {
            RankedPokemonToReturn = Enumerable.Range(1, 20)
                .Select(i => new HubRankedPokemon { Rank = i, Name = $"Pokemon{i}" })
                .ToList()
        };
        var tool = new GetBestPokemonByTypeTool(hubApiService);

        var result = await tool.InvokeAsync("""{"attackerType":"water"}""", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.Equal(10, document.RootElement.EnumerateArray().Count());
    }

    [Fact]
    public async Task InvokeAsync_ReturnsError_WhenAttackerTypeMissing()
    {
        var tool = new GetBestPokemonByTypeTool(new FakeHubApiService());

        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void Definition_HasExpectedFunctionName()
    {
        var tool = new GetBestPokemonByTypeTool(new FakeHubApiService());

        Assert.Equal("get_best_pokemon_by_type", tool.Name);
        Assert.Equal("get_best_pokemon_by_type", tool.Definition.FunctionName);
    }
}
