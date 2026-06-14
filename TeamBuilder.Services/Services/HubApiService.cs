using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Text;
using System.Globalization;
using Microsoft.Playwright;
using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

/// <summary>
/// This service hits the pokemon go hub api to get tier list data
/// </summary>
public class HubApiService(HttpClient httpClient)
{
  private static readonly string[] AttackerTypes =
  [
    "normal", "fire", "water", "electric", "grass", "ice", "fighting", "poison",
    "ground", "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark",
    "steel", "fairy"
  ];

  private readonly HttpClient _httpClient = httpClient;

  public async Task<HubRankingResponse> CrawlAllBestPerTypeAsync(CancellationToken cancellationToken = default)
  {
    var normal = await CrawlBestPerTypeAsync("normal", 200, cancellationToken);
    var fire = await CrawlBestPerTypeAsync("fire", 200, cancellationToken);
    var water = await CrawlBestPerTypeAsync("water", 200, cancellationToken);
    var electric = await CrawlBestPerTypeAsync("electric", 200, cancellationToken);
    var grass = await CrawlBestPerTypeAsync("grass", 200, cancellationToken);
    var ice = await CrawlBestPerTypeAsync("ice", 200, cancellationToken);
    var fighting = await CrawlBestPerTypeAsync("fighting", 200, cancellationToken);
    var poison = await CrawlBestPerTypeAsync("poison", 200, cancellationToken);
    var ground = await CrawlBestPerTypeAsync("ground", 200, cancellationToken);
    var flying = await CrawlBestPerTypeAsync("flying", 200, cancellationToken);
    var psychic = await CrawlBestPerTypeAsync("psychic", 200, cancellationToken);
    var bug = await CrawlBestPerTypeAsync("bug", 200, cancellationToken);
    var rock = await CrawlBestPerTypeAsync("rock", 200, cancellationToken);
    var ghost = await CrawlBestPerTypeAsync("ghost", 200, cancellationToken);
    var dragon = await CrawlBestPerTypeAsync("dragon", 200, cancellationToken);
    var dark = await CrawlBestPerTypeAsync("dark", 200, cancellationToken);
    var steel = await CrawlBestPerTypeAsync("steel", 200, cancellationToken);
    var fairy = await CrawlBestPerTypeAsync("fairy", 200, cancellationToken);

    return new HubRankingResponse
    {
      NormalTierList = normal,
      FireTierList = fire,
      WaterTierList = water,
      ElectricTierList = electric,
      GrassTierList = grass,
      IceTierList = ice,
      FightingTierList = fighting,
      PoisonTierList = poison,
      GroundTierList = ground,
      FlyingTierList = flying,
      PsychicTierList = psychic,
      BugTierList = bug,
      RockTierList = rock,
      GhostTierList = ghost,
      DragonTierList = dragon,
      DarkTierList = dark,
      SteelTierList = steel,
      FairyTierList = fairy
    };
  }

  public static async Task<IList<HubRankedPokemon>> CrawlBestPerTypeAsync(string attackerType, int limit = 200, CancellationToken cancellationToken = default)
  {
    attackerType = attackerType.Trim().ToLowerInvariant();
    var url = $"https://db.pokemongohub.net/pokemon-list/best-per-type/{attackerType}";

    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
      Headless = true
    });

    var context = await browser.NewContextAsync(new BrowserNewContextOptions
    {
      UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36"
    });

    var page = await context.NewPageAsync();
    await page.GotoAsync(url, new PageGotoOptions
    {
      WaitUntil = WaitUntilState.NetworkIdle,
      Timeout = 90000
    });

    await SetRowsLimitAsync(page, limit);
    await page.WaitForTimeoutAsync(1200);

    var pokemon = await ExtractRankedPokemonAsync(page, limit, cancellationToken);

    return pokemon
      .GroupBy(x => $"{x.PokemonNumber}|{x.Name}|{x.Form}")
      .Select(x => x.First())
      .Take(limit)
      .ToList();
  }

  /// <summary>
  /// Sends the raw payload used by Pokemon GO Hub's counters endpoint.
  /// The payload is intentionally a raw string because the endpoint expects text/plain JSON content.
  /// </summary>
  public async Task<HubCountersResponse> GetCountersAsync(string payload = "{}", CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(payload))
    {
      payload = "{}";
    }

    using var request = new HttpRequestMessage(HttpMethod.Post, "api/counters")
    {
      Content = new StringContent(payload, Encoding.UTF8, "text/plain")
    };

    request.Headers.Accept.Clear();
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
    request.Headers.TryAddWithoutValidation("origin", "https://db.pokemongohub.net");
    request.Headers.Referrer = new Uri("https://db.pokemongohub.net/pokemon-list/best-per-type/fighting");
    request.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");
    request.Headers.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36");

    try
    {
      using var response = await _httpClient.SendAsync(request, cancellationToken);
      var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        var isBotBlocked = (int)response.StatusCode == 403;
        return new HubCountersResponse
        {
          StatusCode = (int)response.StatusCode,
          RawJson = rawJson,
          Error = isBotBlocked
            ? "Hub request was blocked (403). PokemonGoHub is likely rejecting server-to-server traffic via bot protection."
            : $"Hub request failed with status {(int)response.StatusCode}."
        };
      }

      var entries = HubHelpers.ExtractCounterEntries(rawJson);
      return new HubCountersResponse
      {
        StatusCode = (int)response.StatusCode,
        RawJson = rawJson,
        Entries = entries
      };
    }
    catch (Exception ex)
    {
      return new HubCountersResponse
      {
        Error = $"Failed to call counters endpoint: {ex.Message}"
      };
    }
  }

  /// <summary>
  /// Matches your uploaded pokemon list against hub ranking entries by pokemon number and form/shadow variant.
  /// </summary>
  public static List<PokemonTierFit> BuildTierFits(IEnumerable<Pokemon> ownedPokemon, IEnumerable<HubCounterEntry> rankings)
  {
    var rankList = rankings.ToList();
    var result = new List<PokemonTierFit>();

    foreach (var pokemon in ownedPokemon)
    {
      if (pokemon.PokemonNumber is null)
      {
        result.Add(new PokemonTierFit
        {
          Pokemon = pokemon,
          MatchStatus = "NoPokemonNumber"
        });
        continue;
      }

      var matches = rankList
        .Where(x => x.PokemonNumber == pokemon.PokemonNumber)
        .ToList();

      if (matches.Count == 0)
      {
        result.Add(new PokemonTierFit
        {
          Pokemon = pokemon,
          MatchStatus = "NotRanked"
        });
        continue;
      }

      var variant = HubHelpers.BuildVariantKey(pokemon.Form, pokemon.ShadowPurified, pokemon.Name);
      var exact = matches
        .Where(x => string.Equals(x.VariantKey, variant, StringComparison.OrdinalIgnoreCase))
        .OrderBy(x => x.Rank ?? int.MaxValue)
        .FirstOrDefault();

      var best = exact ?? matches.OrderBy(x => x.Rank ?? int.MaxValue).First();

      result.Add(new PokemonTierFit
      {
        Pokemon = pokemon,
        MatchStatus = exact is not null ? "MatchedByVariant" : "MatchedByNumber",
        Entry = best
      });
    }

    return result;
  }

  private static async Task SetRowsLimitAsync(IPage page, int limit)
  {
    var optionValue = limit.ToString();
    var selectors = new[]
    {
      "select[name='DataTables_Table_0_length']",
      "select[name$='_length']",
      "select"
    };

    foreach (var selector in selectors)
    {
      var select = page.Locator(selector).First;
      if (await select.CountAsync() == 0)
      {
        continue;
      }

      try
      {
        var options = await select.Locator("option").AllTextContentsAsync();
        if (!options.Any(x => x.Trim() == optionValue))
        {
          continue;
        }

        await select.SelectOptionAsync(new SelectOptionValue { Value = optionValue });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return;
      }
      catch
      {
        // keep trying with next candidate selector
      }
    }
  }

  private static async Task<List<HubRankedPokemon>> ExtractRankedPokemonAsync(IPage page, int limit, CancellationToken cancellationToken)
  {
    var results = new List<HubRankedPokemon>();

    var table = await FindLargestTableAsync(page);
    if (table is null)
    {
      return results;
    }

    var headers = (await table.Locator("thead th").AllTextContentsAsync())
      .Select(x => x.Trim())
      .ToList();

    var rows = table.Locator("tbody tr");
    var rowCount = await rows.CountAsync();

    for (var i = 0; i < rowCount && results.Count < limit; i++)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var row = rows.Nth(i);
      var cellLocator = row.Locator("td");
      var cellCount = await cellLocator.CountAsync();
      if (cellCount == 0)
      {
        continue;
      }

      var cells = new List<string>(cellCount);
      for (var c = 0; c < cellCount; c++)
      {
        var text = await cellLocator.Nth(c).InnerTextAsync();
        cells.Add(text.Trim());
      }

      var rowText = string.Join(" | ", cells);
      var pokemonAnchor = row.Locator("a[href*='/pokemon/']").First;
      var hasPokemonAnchor = await pokemonAnchor.CountAsync() > 0;
      var anchorText = hasPokemonAnchor ? await pokemonAnchor.InnerTextAsync() : null;
      var anchorHref = hasPokemonAnchor ? await pokemonAnchor.GetAttributeAsync("href") : null;

      var identity = ParsePokemonIdentityFromHref(anchorHref);
      if (identity is null)
      {
        continue;
      }

      var name = !string.IsNullOrWhiteSpace(anchorText)
        ? anchorText.Trim()
        : GetCellByHeader(headers, cells, "pokemon", "name") ?? GuessName(cells);

      if (string.IsNullOrWhiteSpace(name))
      {
        continue;
      }

      var pokemonNumber = identity.Value.Number;

      var rank = ParseInt(GetCellByHeader(headers, cells, "rank"))
        ?? ParseInt(cells.FirstOrDefault())
        ?? (i + 1);
      var form = identity.Value.Form;
      var fastAttack = GetCellByHeader(headers, cells, "fast", "quick")
        ?? GetCellByPosition(cells, 2);
      var chargedAttack = GetCellByHeader(headers, cells, "charged")
        ?? GetCellByPosition(cells, 3);
      var dps = ParseDouble(GetCellByExactHeader(headers, cells, "dps"))
        ?? ParseDouble(GetCellByPosition(cells, 4));
      var tdo = ParseDouble(GetCellByExactHeader(headers, cells, "tdo"))
        ?? ParseDouble(GetCellByPosition(cells, 5));
      var score = ParseDouble(GetCellByExactHeader(headers, cells, "score"))
        ?? ParseDouble(GetCellByPosition(cells, 6));

      results.Add(new HubRankedPokemon
      {
        Rank = rank,
        Name = CleanName(name),
        Form = form,
        PokemonNumber = pokemonNumber,
        FastAttack = fastAttack,
        ChargedAttack = chargedAttack,
        DPS = dps,
        TDO = tdo,
        Score = score
      });
    }

    return results;
  }

  private static async Task<ILocator?> FindLargestTableAsync(IPage page)
  {
    var tables = page.Locator("table");
    var tableCount = await tables.CountAsync();

    ILocator? selected = null;
    var maxRows = -1;

    for (var i = 0; i < tableCount; i++)
    {
      var table = tables.Nth(i);
      var rows = await table.Locator("tbody tr").CountAsync();
      if (rows > maxRows)
      {
        maxRows = rows;
        selected = table;
      }
    }

    return selected;
  }

  private static string? GetCellByHeader(IReadOnlyList<string> headers, IReadOnlyList<string> cells, params string[] headerContains)
  {
    for (var i = 0; i < headers.Count && i < cells.Count; i++)
    {
      var header = headers[i].ToLowerInvariant();
      if (headerContains.Any(h => header.Contains(h, StringComparison.OrdinalIgnoreCase)))
      {
        return cells[i];
      }
    }

    return null;
  }

  private static string? GetCellByExactHeader(IReadOnlyList<string> headers, IReadOnlyList<string> cells, string headerName)
  {
    for (var i = 0; i < headers.Count && i < cells.Count; i++)
    {
      if (string.Equals(headers[i].Trim(), headerName, StringComparison.OrdinalIgnoreCase))
      {
        return cells[i];
      }
    }

    return null;
  }

  private static string? GetCellByPosition(IReadOnlyList<string> cells, int index)
  {
    if (index < 0 || index >= cells.Count)
    {
      return null;
    }

    var value = cells[index]?.Trim();
    return string.IsNullOrWhiteSpace(value) ? null : value;
  }

  private static string? GuessName(IReadOnlyList<string> cells)
  {
    return cells.FirstOrDefault(x => x.Any(char.IsLetter) && x.Length > 1 && !x.Contains('%') && !x.Contains("CP", StringComparison.OrdinalIgnoreCase));
  }

  private static string? GuessForm(string? name, string rowText)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return null;
    }

    var m = Regex.Match(name, @"\(([^)]+)\)");
    if (m.Success)
    {
      return m.Groups[1].Value.Trim();
    }

    var forms = new[] { "Normal", "Shadow", "Alola", "Galar", "Hisui", "Paldea", "Purified", "Mega" };
    return forms.FirstOrDefault(f => rowText.Contains(f, StringComparison.OrdinalIgnoreCase));
  }

  private static string CleanName(string name)
  {
    return Regex.Replace(name, @"\s*\([^)]*\)\s*", string.Empty).Trim();
  }

  private static int? ParseDexFromText(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return null;
    }

    var match = Regex.Match(text, @"#\s*(\d{1,4})");
    if (!match.Success)
    {
      return null;
    }

    return int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : null;
  }

  private static (int Number, string Form)? ParsePokemonIdentityFromHref(string? href)
  {
    if (string.IsNullOrWhiteSpace(href))
    {
      return null;
    }

    var match = Regex.Match(href, @"/pokemon/(?<num>\d{1,4})(?:-(?<form>[a-z0-9-]+))?$", RegexOptions.IgnoreCase);
    if (!match.Success)
    {
      return null;
    }

    if (!int.TryParse(match.Groups["num"].Value, out var parsed))
    {
      return null;
    }

    var formSlug = match.Groups["form"].Success
      ? match.Groups["form"].Value
      : "normal";

    return (parsed, SlugToTitleCase(formSlug));
  }

  private static string SlugToTitleCase(string slug)
  {
    if (string.IsNullOrWhiteSpace(slug))
    {
      return "Normal";
    }

    var words = slug
      .Split('-', StringSplitOptions.RemoveEmptyEntries)
      .Select(x => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x.ToLowerInvariant()));

    var combined = string.Join(' ', words);
    return string.IsNullOrWhiteSpace(combined) ? "Normal" : combined;
  }

  private static int? ParseInt(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    var digits = Regex.Match(value, @"-?\d+");
    return digits.Success && int.TryParse(digits.Value, out var parsed)
      ? parsed
      : null;
  }

  private static double? ParseDouble(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    var match = Regex.Match(value, @"-?\d+(?:[\.,]\d+)?");
    if (!match.Success)
    {
      return null;
    }

    var normalized = match.Value.Replace(',', '.');
    return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
      ? parsed
      : null;
  }
}
