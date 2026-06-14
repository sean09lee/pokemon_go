using System.Globalization;
using System.Text.Json;
using TeamBuilder.Data.Models;

namespace TeamBuilder.Services.Helpers;

internal class HubHelpers
{
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
