using System.Text.Json;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Tests;

public class HubHelpersTests
{
    [Fact]
    public void ParseRankedPokemonSnapshot_PreservesRankingAndForm()
    {
        using var document = JsonDocument.Parse("""
            {
                "headers": ["Rank", "Pokemon", "Fast", "Charged", "DPS", "TDO", "Score"],
                "rows": [{
                    "cells": ["1", "Regigigas (Shadow)", "Hidden Power", "Giga Impact", "25.5", "500", "100"],
                    "anchorText": "Regigigas (Shadow)",
                    "anchorHref": "/pokemon/486-shadow?view=raid"
                }]
            }
            """);

        var pokemon = Assert.Single(HubHelpers.ParseRankedPokemonSnapshot(document.RootElement, 200, CancellationToken.None));

        Assert.Equal(1, pokemon.Rank);
        Assert.Equal(486, pokemon.PokemonNumber);
        Assert.Equal("Regigigas", pokemon.Name);
        Assert.Equal("Shadow", pokemon.Form);
        Assert.Equal("Hidden Power", pokemon.FastAttack);
        Assert.Equal("Giga Impact", pokemon.ChargedAttack);
        Assert.Equal(25.5, pokemon.DPS);
        Assert.Equal(500, pokemon.TDO);
        Assert.Equal(100, pokemon.Score);
    }

    [Fact]
    public void ParseRankedPokemonSnapshot_SkipsInvalidRowsAndUsesFallbacks()
    {
        using var document = JsonDocument.Parse("""
            {
                "headers": [],
                "rows": [
                    {"cells": [], "anchorText": null, "anchorHref": null},
                    {"cells": ["Advert"], "anchorText": null, "anchorHref": "/other"},
                    {"cells": ["", "Ditto", "Transform", "Struggle", "1,5", "25", "3"], "anchorText": "", "anchorHref": "/pokemon/132"}
                ]
            }
            """);

        var pokemon = Assert.Single(HubHelpers.ParseRankedPokemonSnapshot(document.RootElement, 200, CancellationToken.None));

        Assert.Equal(3, pokemon.Rank);
        Assert.Equal("Ditto", pokemon.Name);
        Assert.Equal("Normal", pokemon.Form);
        Assert.Equal(1.5, pokemon.DPS);
    }

    [Fact]
    public void ParseRankedPokemonSnapshot_RespectsLimit()
    {
        var row = new { cells = new[] { "1", "Ditto" }, anchorText = "Ditto", anchorHref = "/pokemon/132" };
        var snapshot = JsonSerializer.SerializeToElement(new { headers = Array.Empty<string>(), rows = new[] { row, row } });

        Assert.Single(HubHelpers.ParseRankedPokemonSnapshot(snapshot, 1, CancellationToken.None));
    }

    [Fact]
    public void ParseRankedPokemonSnapshot_RespectsCancellation()
    {
        var snapshot = JsonSerializer.SerializeToElement(new { headers = Array.Empty<string>(), rows = Array.Empty<object>() });

        Assert.Throws<OperationCanceledException>(() =>
            HubHelpers.ParseRankedPokemonSnapshot(snapshot, 200, new CancellationToken(canceled: true)));
    }
}