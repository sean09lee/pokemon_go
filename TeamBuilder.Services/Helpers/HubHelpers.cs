using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TeamBuilder.Data.Models;

namespace TeamBuilder.Services.Helpers;

internal class HubHelpers
{
    internal static async Task SetRowsLimitAsync(IPage page, int limit)
    {
        var optionValue = limit.ToString();
        var selectors = new[]
        {
            "select[class*='DataGrid_paginationControl']",
            "select.DataGrid_paginationControl__fVEx5",
            "select[name='DataTables_Table_0_length']",
            "select[name$='_length']",
            "select"
        };

        var previousRowCount = await page.Locator("table tbody tr").CountAsync();

        foreach (var selector in selectors)
        {
            var selectCandidates = page.Locator(selector);
            var candidateCount = await selectCandidates.CountAsync();
            if (candidateCount == 0)
            {
                continue;
            }

            for (var i = 0; i < candidateCount; i++)
            {
                var select = selectCandidates.Nth(i);

                try
                {
                    var optionWithValue = select.Locator($"option[value='{optionValue}']");
                    var hasValueOption = await optionWithValue.CountAsync() > 0;
                    var options = await select.Locator("option").AllTextContentsAsync();
                    var hasTextOption = options.Any(x => x.Trim().EndsWith(optionValue, StringComparison.Ordinal));
                    if (!hasValueOption && !hasTextOption)
                    {
                        continue;
                    }

                    if (hasValueOption)
                    {
                        await select.SelectOptionAsync(new SelectOptionValue { Value = optionValue });
                    }
                    else
                    {
                        await select.SelectOptionAsync(new SelectOptionValue { Label = $"Show {optionValue}" });
                    }

                    try
                    {
                        await page.WaitForFunctionAsync(
                            "(prev) => document.querySelectorAll('table tbody tr').length !== prev",
                            previousRowCount,
                            new PageWaitForFunctionOptions { Timeout = 8000 });
                    }
                    catch
                    {
                        // table may already be fully loaded or unchanged; continue
                    }

                    await page.WaitForTimeoutAsync(500);
                    return;
                }
                catch
                {
                    // keep trying next candidate select
                }
            }
        }
    }

    internal static async Task<List<HubRankedPokemon>> ExtractRankedPokemonAsync(IPage page, int limit, CancellationToken cancellationToken)
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

    internal static async Task<ILocator?> FindLargestTableAsync(IPage page)
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

    internal static string? GetCellByHeader(IReadOnlyList<string> headers, IReadOnlyList<string> cells, params string[] headerContains)
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

    internal static string? GetCellByExactHeader(IReadOnlyList<string> headers, IReadOnlyList<string> cells, string headerName)
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

    internal static string? GetCellByPosition(IReadOnlyList<string> cells, int index)
    {
        if (index < 0 || index >= cells.Count)
        {
            return null;
        }

        var value = cells[index]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    internal static string? GuessName(IReadOnlyList<string> cells)
    {
        return cells.FirstOrDefault(x => x.Any(char.IsLetter) && x.Length > 1 && !x.Contains('%') && !x.Contains("CP", StringComparison.OrdinalIgnoreCase));
    }

    internal static string CleanName(string name)
    {
        return Regex.Replace(name, @"\s*\([^)]*\)\s*", string.Empty).Trim();
    }

    internal static (int Number, string Form)? ParsePokemonIdentityFromHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var normalizedHref = href.Trim();
        var queryIndex = normalizedHref.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            normalizedHref = normalizedHref[..queryIndex];
        }

        normalizedHref = normalizedHref.TrimEnd('/');

        var match = Regex.Match(normalizedHref, @"/pokemon/(?<num>\d{1,4})(?:-(?<form>[a-z0-9_-]+))?$", RegexOptions.IgnoreCase);
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

    internal static string SlugToTitleCase(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return "Normal";
        }

        var words = slug
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x.ToLowerInvariant()));

        var combined = string.Join(' ', words);
        return string.IsNullOrWhiteSpace(combined) ? "Normal" : combined;
    }

    internal static int? ParseInt(string? value)
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

    internal static double? ParseDouble(string? value)
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

    internal static List<HubCounterEntry> ExtractCounterEntries(string rawJson)
    {
        var entries = new List<HubCounterEntry>();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return entries;
        }

        using var document = JsonDocument.Parse(rawJson);
        TraverseJson(document.RootElement, entries);

        return entries
            .GroupBy(x => $"{x.PokemonNumber}|{x.VariantKey}|{x.Rank}|{x.Tier}")
            .Select(x => x.First())
            .ToList();
    }

    internal static void TraverseJson(JsonElement element, ICollection<HubCounterEntry> entries)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    TraverseJson(item, entries);
                }
                break;

            case JsonValueKind.Object:
                if (TryParseCounterEntry(element, out var entry))
                {
                    entries.Add(entry);
                }

                foreach (var property in element.EnumerateObject())
                {
                    TraverseJson(property.Value, entries);
                }
                break;
        }
    }

    internal static bool TryParseCounterEntry(JsonElement element, out HubCounterEntry entry)
    {
        entry = new HubCounterEntry();

        var pokemonNumber = GetIntByKeys(element,
            "pokemonNumber", "pokemon_number", "dex", "id", "pokemon_id", "species_id");

        var rank = GetIntByKeys(element, "rank", "overallRank", "position", "counterRank");
        var name = GetStringByKeys(element, "name", "pokemonName", "pokemon_name", "speciesName");
        var form = GetStringByKeys(element, "form", "pokemonForm", "pokemon_form");
        var tier = GetStringByKeys(element, "tier", "grade", "scoreTier");
        var score = GetDecimalByKeys(element, "score", "rating", "dps", "tdo");
        var isShadow = GetBoolByKeys(element, "isShadow", "shadow");

        var hasAnyIdentity = pokemonNumber is not null || !string.IsNullOrWhiteSpace(name);
        var hasRankingSignal = rank is not null || !string.IsNullOrWhiteSpace(tier) || score is not null;

        if (!hasAnyIdentity || !hasRankingSignal)
        {
            return false;
        }

        entry = new HubCounterEntry
        {
            PokemonNumber = pokemonNumber,
            Name = name,
            Form = form,
            IsShadow = isShadow,
            Rank = rank,
            Tier = tier,
            Score = score,
            VariantKey = BuildVariantKey(form, isShadow, name)
        };

        return true;
    }

    internal static string BuildVariantKey(string? form, int? shadowPurified, string? name)
    {
        var shadow = (shadowPurified ?? 0) > 0 || ContainsText(form, "shadow") || ContainsText(name, "shadow");
        return BuildVariantKey(form, shadow ? true : null, name);
    }

    internal static string BuildVariantKey(string? form, bool? isShadow, string? name)
    {
        if (isShadow == true || ContainsText(form, "shadow") || ContainsText(name, "shadow"))
        {
            return "shadow";
        }

        if (ContainsText(form, "alola") || ContainsText(form, "alolan") || ContainsText(name, "alola"))
        {
            return "alola";
        }

        if (ContainsText(form, "galar") || ContainsText(name, "galar"))
        {
            return "galar";
        }

        if (ContainsText(form, "hisui") || ContainsText(name, "hisui"))
        {
            return "hisui";
        }

        if (ContainsText(form, "paldea") || ContainsText(name, "paldea"))
        {
            return "paldea";
        }

        if (ContainsText(form, "mega") || ContainsText(name, "mega"))
        {
            return "mega";
        }

        if (!string.IsNullOrWhiteSpace(form) && !string.Equals(form, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            return form.Trim().ToLowerInvariant();
        }

        return "normal";
    }

    internal static bool ContainsText(string? value, string text) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(text, StringComparison.OrdinalIgnoreCase);

    internal static string? GetStringByKeys(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return value.ToString();
            }
        }

        return null;
    }

    internal static int? GetIntByKeys(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    internal static decimal? GetDecimalByKeys(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var decValue))
            {
                return decValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    internal static bool? GetBoolByKeys(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            {
                return intValue != 0;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (bool.TryParse(text, out var boolValue))
                {
                    return boolValue;
                }

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numberValue))
                {
                    return numberValue != 0;
                }
            }
        }

        return null;
    }
}
