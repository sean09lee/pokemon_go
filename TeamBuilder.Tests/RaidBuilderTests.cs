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
    public async Task RaidTeams_UseAtMostOneMega_AndPreferHighestRankedMega()
    {
        var ownedPokemon = new List<Pokemon>
        {
            new() { Index = 1, Name = "Charmander A", Form = "Normal", PokemonNumber = 4, CP = 826, HP = 82, AtkIV = 15, IvAvg = 98m, LevelMin = 20m },
            new() { Index = 2, Name = "Charmander B", Form = "Normal", PokemonNumber = 4, CP = 532, HP = 65, AtkIV = 13, IvAvg = 90m, LevelMin = 15m },
            new() { Index = 3, Name = "Charizard", Form = "Normal", PokemonNumber = 6, CP = 2400, HP = 150, AtkIV = 14, IvAvg = 93m, LevelMin = 30m }
        };

        var rankingService = CreateRankingService();
        var hubRanking = BuildHubRankingForSingleMegaScenario();

        var response = await rankingService.BuildRankingResponseAsync(
            ownedPokemon,
            hubRanking,
            dustLimit: 100000,
            candyByPokemonNumber: new Dictionary<int, int>
            {
                [4] = 1000
            });

        Assert.True(response.RaidTeamsByType.ContainsKey("fire"));
        var teams = new[]
        {
            response.RaidTeamsByType["fire"].NoInvestmentTeam,
            response.RaidTeamsByType["fire"].CurrentResourcesTeam,
            response.RaidTeamsByType["fire"].UnlimitedResourcesTeam
        };

        foreach (var team in teams)
        {
            var megaMembers = team
                .Where(x => ((x.EvolvedForm ?? string.Empty).IndexOf("mega", StringComparison.OrdinalIgnoreCase) >= 0)
                    || ((x.EvolvedName ?? string.Empty).IndexOf("mega", StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            Assert.True(
                megaMembers.Count <= 1,
                $"Expected at most one mega in team. Team={string.Join(" | ", team.Select(x => $"#{x.PokemonNumber}:{x.Name}->{x.EvolvedName}({x.EvolvedForm}) r{x.Index}"))}");
        }

        var currentMega = response.RaidTeamsByType["fire"].CurrentResourcesTeam
            .FirstOrDefault(x => string.Equals(x.EvolvedForm, "Mega X", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.EvolvedForm, "Mega Y", StringComparison.OrdinalIgnoreCase));
        var unlimitedMega = response.RaidTeamsByType["fire"].UnlimitedResourcesTeam
            .FirstOrDefault(x => string.Equals(x.EvolvedForm, "Mega X", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.EvolvedForm, "Mega Y", StringComparison.OrdinalIgnoreCase));

        Assert.True(currentMega is not null, "Expected Current Resources team to include a mega when one is available.");
        Assert.True(unlimitedMega is not null, "Expected Unlimited Resources team to include a mega when one is available.");
        Assert.Equal("Mega X", currentMega?.EvolvedForm);
        Assert.Equal(1, currentMega?.Index);
        Assert.Equal("Mega X", unlimitedMega?.EvolvedForm);
        Assert.Equal(1, unlimitedMega?.Index);
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

    [Fact]
    public async Task CurrentResourcesTeam_DoesNotTargetEvolution_WhenCandyIsInsufficient()
    {
        var ownedPokemon = new List<Pokemon>
        {
            new() { Index = 1, Name = "Charmander A", Form = "Normal", PokemonNumber = 4, CP = 826, HP = 82, AtkIV = 15, IvAvg = 98m, LevelMin = 20m },
            new() { Index = 2, Name = "Charmander B", Form = "Normal", PokemonNumber = 4, CP = 532, HP = 65, AtkIV = 13, IvAvg = 90m, LevelMin = 15m },
            new() { Index = 3, Name = "Charizard", Form = "Normal", PokemonNumber = 6, CP = 2400, HP = 150, AtkIV = 14, IvAvg = 93m, LevelMin = 30m }
        };

        var rankingService = CreateRankingService();
        var hubRanking = BuildHubRankingForEvolutionCandyScenario();

        var response = await rankingService.BuildRankingResponseAsync(
            ownedPokemon,
            hubRanking,
            dustLimit: 0,
            candyByPokemonNumber: new Dictionary<int, int>
            {
                [4] = 0
            });

        Assert.True(response.RaidTeamsByType.ContainsKey("fire"));
        var current = response.RaidTeamsByType["fire"].CurrentResourcesTeam;

        var charmanderMembers = current.Where(x => x.PokemonNumber == 4).ToList();
        Assert.NotEmpty(charmanderMembers);
        Assert.All(charmanderMembers, x => Assert.True(
            x.EvolvedPokemonNumber is null,
            $"Charmander should not be assigned to an evolved target without candy. Got target #{x.EvolvedPokemonNumber}."));
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

    private static HubRankingResponse BuildHubRankingForEvolutionCandyScenario()
    {
        return new HubRankingResponse
        {
            FireTierList =
            [
                new HubRankedPokemon { Rank = 1, Name = "Charizard", Form = "Normal", PokemonNumber = 6 },
                new HubRankedPokemon { Rank = 2, Name = "Charmeleon", Form = "Normal", PokemonNumber = 5 },
                new HubRankedPokemon { Rank = 3, Name = "Charmander", Form = "Normal", PokemonNumber = 4 }
            ]
        };
    }

    private static HubRankingResponse BuildHubRankingForSingleMegaScenario()
    {
        return new HubRankingResponse
        {
            FireTierList =
            [
                new HubRankedPokemon { Rank = 1, Name = "Charizard", Form = "Mega X", PokemonNumber = 6 },
                new HubRankedPokemon { Rank = 2, Name = "Charizard", Form = "Mega Y", PokemonNumber = 6 },
                new HubRankedPokemon { Rank = 3, Name = "Charizard", Form = "Normal", PokemonNumber = 6 },
                new HubRankedPokemon { Rank = 4, Name = "Charmeleon", Form = "Normal", PokemonNumber = 5 },
                new HubRankedPokemon { Rank = 5, Name = "Charmander", Form = "Normal", PokemonNumber = 4 }
            ]
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
