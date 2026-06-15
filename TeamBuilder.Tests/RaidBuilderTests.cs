using System.Net;
using System.Text;
using System.Text.Json;
using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Helpers;
using TeamBuilder.Services.Services;

namespace TeamBuilder.Tests;

public class RaidBuilderTests
{
    [Fact]
    public async Task ParsePokemonCsv_FillsMissingForms_FromPokemonNameHints()
    {
        var csvPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TeamBuilder.Tests", "Data", "test_pokemon.csv"));
        await using var stream = File.OpenRead(csvPath);
        var parseResult = await CsvParserService.ParsePokemonCsvAsync(stream);

        Assert.True(string.IsNullOrWhiteSpace(parseResult.FatalError), parseResult.FatalError);
        Assert.NotEmpty(parseResult.Pokemon);

        Assert.DoesNotContain(parseResult.Pokemon, p => string.IsNullOrWhiteSpace(p.Form));

        var shadowByName = parseResult.Pokemon
            .Where(p => (p.Name ?? string.Empty).Contains("Shadow", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var p in shadowByName)
        {
            Assert.Equal("Shadow", p.Form);
        }

        var alolaByName = parseResult.Pokemon
            .Where(p =>
                (p.Name ?? string.Empty).Contains("Alolan", StringComparison.OrdinalIgnoreCase)
                || (p.Name ?? string.Empty).Contains("Alola", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var p in alolaByName)
        {
            Assert.Equal("Alola", p.Form);
        }
    }

    [Fact]
    public async Task UnlimitedResourcesFireTeam_UsesTwoCharmanderSlots_ForMegaXAndMegaY()
    {
        var csvPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TeamBuilder.Tests", "Data", "test_pokemon.csv"));
        await using var stream = File.OpenRead(csvPath);
        var parseResult = await CsvParserService.ParsePokemonCsvAsync(stream);

        Assert.True(string.IsNullOrWhiteSpace(parseResult.FatalError), parseResult.FatalError);
        Assert.NotEmpty(parseResult.Pokemon);

        var charmanders = parseResult.Pokemon
            .Where(x => x.PokemonNumber == 4 && string.Equals(x.Form, "Normal", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(charmanders.Count >= 2, "test_pokemon.csv must contain at least two normal Charmander entries.");
        Assert.All(charmanders, x => Assert.Equal("Normal", x.Form));

        var rankingService = CreateRankingService();
        var hubRanking = BuildHubRankingForFireScenario();

        var response = await rankingService.BuildRankingResponseAsync(
            parseResult.Pokemon,
            hubRanking,
            dustLimit: 100000,
            candyByPokemonNumber: new Dictionary<int, int>());

        Assert.True(response.RaidTeamsByType.ContainsKey("fire"));
        var fireUnlimited = response.RaidTeamsByType["fire"].UnlimitedResourcesTeam;
        var snapshot = string.Join(" | ", fireUnlimited.Select(x => $"#{x.PokemonNumber}:{x.Name}->{x.EvolvedName}({x.EvolvedForm}) r{x.Index}"));

        var familySlots = fireUnlimited.Count(x => x.PokemonNumber is 4 or 5 or 6);
        Assert.True(familySlots == 2, $"Expected 2 charmander-family slots. Actual={familySlots}. Team={snapshot}");

        var charmanderTargets = fireUnlimited
            .Where(x => x.PokemonNumber == 4)
            .Select(x => new
            {
                OwnedIdentity = $"{x.PokemonNumber}|{x.Form}|{x.CP}|{x.HP}",
                OwnedCp = x.CP,
                EvolvedName = x.EvolvedName ?? string.Empty,
                MegaToken = NormalizeMegaFormToken(x.EvolvedForm)
            })
            .ToList();

        Assert.True(charmanderTargets.Count == 2, $"Expected 2 Charmander entries. Actual={charmanderTargets.Count}. Team={snapshot}");
        Assert.True(
            charmanderTargets.Select(x => x.OwnedIdentity).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2,
            $"Expected 2 distinct owned Charmander entries, but the same one was reused. Team={snapshot}");
        Assert.All(charmanderTargets, t => Assert.Equal("Charizard", t.EvolvedName));

        var selectedCps = charmanderTargets.Select(x => x.OwnedCp).OrderBy(x => x).ToList();
        Assert.Equal(new List<int?> { 532, 826 }, selectedCps);

        var targetForms = charmanderTargets.Select(x => x.MegaToken).OrderBy(x => x).ToList();
        Assert.All(targetForms, form => Assert.Contains(form, new[] { "x", "y" }));
        if (targetForms.Count == 2)
        {
            Assert.Equal(new List<string> { "x", "y" }, targetForms);
        }
    }

    [Fact]
    public async Task NormalKrabby_DoesNotTargetShadowKingler()
    {
        var csvPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TeamBuilder.Tests", "Data", "test_pokemon.csv"));
        await using var stream = File.OpenRead(csvPath);
        var parseResult = await CsvParserService.ParsePokemonCsvAsync(stream);

        Assert.True(string.IsNullOrWhiteSpace(parseResult.FatalError), parseResult.FatalError);

        var hasRegularKrabby = parseResult.Pokemon.Any(x => x.PokemonNumber == 98 && string.Equals(x.Form, "Normal", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasRegularKrabby);

        var rankingService = CreateRankingService();
        var hub = BuildHubRankingForKrabbyScenario();

        var response = await rankingService.BuildRankingResponseAsync(
            parseResult.Pokemon,
            hub,
            dustLimit: 100000,
            candyByPokemonNumber: new Dictionary<int, int>());

        Assert.True(response.RaidTeamsByType.ContainsKey("water"));
        var waterTeams = response.RaidTeamsByType["water"];
        var allWater = waterTeams.NoInvestmentTeam
            .Concat(waterTeams.CurrentResourcesTeam)
            .Concat(waterTeams.UnlimitedResourcesTeam)
            .Where(x => x.PokemonNumber == 98)
            .ToList();

        foreach (var entry in allWater)
        {
            Assert.False(string.Equals(entry.EvolvedForm, "Shadow", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task UnlimitedResourcesGrassTeam_HasOnlyOneRoseradeRankedTarget()
    {
        var csvPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TeamBuilder.Tests", "Data", "test_pokemon.csv"));
        await using var stream = File.OpenRead(csvPath);
        var parseResult = await CsvParserService.ParsePokemonCsvAsync(stream);

        Assert.True(string.IsNullOrWhiteSpace(parseResult.FatalError), parseResult.FatalError);

        var rankingService = CreateRankingService();
        var hubRanking = BuildHubRankingForGrassScenario();

        var response = await rankingService.BuildRankingResponseAsync(
            parseResult.Pokemon,
            hubRanking,
            dustLimit: 100000,
            candyByPokemonNumber: new Dictionary<int, int>());

        Assert.True(response.RaidTeamsByType.ContainsKey("grass"));
        var grassUnlimited = response.RaidTeamsByType["grass"].UnlimitedResourcesTeam;
        var snapshot = string.Join(" | ", grassUnlimited.Select(x => $"#{x.PokemonNumber}:{x.Name}->{x.EvolvedName}({x.EvolvedForm}) r{x.Index}"));

        var roseradeTargetCount = grassUnlimited.Count(x => x.EvolvedPokemonNumber == 407);
        Assert.True(
            roseradeTargetCount == 1,
            $"Expected exactly one Unlimited grass team member to target Roserade (#407). Actual={roseradeTargetCount}. Team={snapshot}");
    }

    private static RankingService CreateRankingService()
    {
        var handler = new FakePogoApiHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://pogoapi.net/")
        };

        var helper = new PogoHelpers(httpClient);
        var pogoApi = new PogoApiService(helper);
        return new RankingService(pogoApi);
    }

    private static string ReadTestDataJson(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "TeamBuilder.Tests",
            "Data",
            fileName));

        return File.ReadAllText(path);
    }

    private static HubRankingResponse BuildHubRankingForFireScenario()
    {
        return new HubRankingResponse
        {
            FireTierList = ReadHubCacheTierList("hub-best-per-type-fire-200.json")
        };
    }

    private static HubRankingResponse BuildHubRankingForKrabbyScenario()
    {
        return new HubRankingResponse
        {
            WaterTierList = ReadHubCacheTierList("hub-best-per-type-water-200.json")
        };
    }

    private static HubRankingResponse BuildHubRankingForGrassScenario()
    {
        return new HubRankingResponse
        {
            GrassTierList = ReadHubCacheTierList("hub-best-per-type-grass-200.json")
        };
    }

    private static IList<HubRankedPokemon> ReadHubCacheTierList(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "TeamBuilder.Data",
            "Cache",
            fileName));

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<HubRankedPokemon>>(json, options) ?? [];
    }

    private static string NormalizeMegaFormToken(string? form)
    {
        var f = (form ?? string.Empty).Trim().ToLowerInvariant();
        if (f is "x" or "mega x")
        {
            return "x";
        }

        if (f is "y" or "mega y")
        {
            return "y";
        }

        return f;
    }

    private sealed class FakePogoApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath.Replace("\\", "/") ?? string.Empty;
            string json;

            if (path.EndsWith("/api/v1/pokemon_evolutions.json", StringComparison.OrdinalIgnoreCase))
            {
                json = ReadTestDataJson("pokemon_evolutions.json");
            }
            else if (path.EndsWith("/api/v1/mega_pokemon.json", StringComparison.OrdinalIgnoreCase))
            {
                json = ReadTestDataJson("mega_pokemon.json");
            }
            else if (path.EndsWith("/api/v1/pokemon_types.json", StringComparison.OrdinalIgnoreCase))
            {
                json = ReadTestDataJson("pokemon_types.json");
            }
            else if (path.EndsWith("/api/v1/pokemon_powerup_requirements.json", StringComparison.OrdinalIgnoreCase))
            {
                json = ReadTestDataJson("pokemon_powerup_requirements.json");
            }
            else
            {
                json = "[]";
            }

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(message);
        }
    }
}
