using System.Globalization;
using TeamBuilder.Data.Enums;
using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;

namespace TeamBuilder.Services.Helpers;

internal class CsvHelpers
{
    internal static Dictionary<string, int> BuildHeaderIndex(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i]?.Trim();
            if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
            {
                map.Add(header, i);
            }
        }

        return map;
    }

    internal static Pokemon ParsePokemon(
        string[] row,
        IReadOnlyDictionary<string, int> header,
        int rowNumber,
        ICollection<CsvValidationFailure> failures)
    {
        var name = GetString(row, header, "Name");
        var form = GetString(row, header, "Form");

        return new Pokemon
        {
            Index = GetInt(row, header, "Index", rowNumber, failures),
            Name = name,
            Form = InferForm(name, form),
            PokemonNumber = GetInt(row, header, "Pokemon Number", rowNumber, failures),
            Gender = GetString(row, header, "Gender"),
            CP = GetInt(row, header, "CP", rowNumber, failures),
            HP = GetInt(row, header, "HP", rowNumber, failures),
            AtkIV = GetInt(row, header, "Atk IV", rowNumber, failures),
            DefIV = GetInt(row, header, "Def IV", rowNumber, failures),
            StaIV = GetInt(row, header, "Sta IV", rowNumber, failures),
            IvAvg = GetDecimal(row, header, "IV Avg", rowNumber, failures),
            LevelMin = GetDecimal(row, header, "Level Min", rowNumber, failures),
            LevelMax = GetDecimal(row, header, "Level Max", rowNumber, failures),
            QuickMove = GetString(row, header, "Quick Move"),
            ChargeMove = GetString(row, header, "Charge Move"),
            ChargeMove2 = GetString(row, header, "Charge Move 2"),
            ScanDate = GetDateTime(row, header, "Scan Date", rowNumber, failures),
            OriginalScanDate = GetDateTime(row, header, "Original Scan Date", rowNumber, failures),
            CatchDate = GetDateTime(row, header, "Catch Date", rowNumber, failures),
            Weight = GetString(row, header, "Weight"),
            Height = GetString(row, header, "Height"),
            Lucky = GetInt(row, header, "Lucky", rowNumber, failures),
            ShadowPurified = GetInt(row, header, "Shadow/Purified", rowNumber, failures),
            Favorite = GetInt(row, header, "Favorite", rowNumber, failures),
            Dust = GetInt(row, header, "Dust", rowNumber, failures),
            RankPercentG = GetString(row, header, "Rank % (G)"),
            RankNumberG = GetInt(row, header, "Rank # (G)", rowNumber, failures),
            StatProdG = GetString(row, header, "Stat Prod (G)"),
            DustCostG = GetInt(row, header, "Dust Cost (G)", rowNumber, failures),
            CandyCostG = GetInt(row, header, "Candy Cost (G)", rowNumber, failures),
            NameG = GetString(row, header, "Name (G)"),
            FormG = GetString(row, header, "Form (G)"),
            ShaPurG = GetInt(row, header, "Sha/Pur (G)", rowNumber, failures),
            RankPercentU = GetString(row, header, "Rank % (U)"),
            RankNumberU = GetInt(row, header, "Rank # (U)", rowNumber, failures),
            StatProdU = GetString(row, header, "Stat Prod (U)"),
            DustCostU = GetInt(row, header, "Dust Cost (U)", rowNumber, failures),
            CandyCostU = GetInt(row, header, "Candy Cost (U)", rowNumber, failures),
            NameU = GetString(row, header, "Name (U)"),
            FormU = GetString(row, header, "Form (U)"),
            ShaPurU = GetInt(row, header, "Sha/Pur (U)", rowNumber, failures),
            RankPercentL = GetString(row, header, "Rank % (L)"),
            RankNumberL = GetInt(row, header, "Rank # (L)", rowNumber, failures),
            StatProdL = GetString(row, header, "Stat Prod (L)"),
            DustCostL = GetInt(row, header, "Dust Cost (L)", rowNumber, failures),
            CandyCostL = GetInt(row, header, "Candy Cost (L)", rowNumber, failures),
            NameL = GetString(row, header, "Name (L)"),
            FormL = GetString(row, header, "Form (L)"),
            ShaPurL = GetInt(row, header, "Sha/Pur (L)", rowNumber, failures),
            MarkedForPvpUse = GetString(row, header, "Marked for PvP use")
        };
    }

    private static string InferForm(string? name, string? form)
    {
        if (!string.IsNullOrWhiteSpace(form))
        {
            return CanonicalizeForm(form.Trim());
        }

        // If form is not provided, attempt to infer it from the name
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Contains(nameof(PokemonForms.Shadow), StringComparison.OrdinalIgnoreCase))
        {
            return nameof(PokemonForms.Shadow);
        }

        if (normalizedName.Contains(nameof(PokemonForms.Alola), StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Contains("alolan", StringComparison.OrdinalIgnoreCase))
        {
            return nameof(PokemonForms.Alola);
        }

        if (normalizedName.Contains(nameof(PokemonForms.Mega), StringComparison.OrdinalIgnoreCase))
        {
            return nameof(PokemonForms.Mega);
        }

        return nameof(PokemonForms.Normal);
    }

    private static string CanonicalizeForm(string form)
    {
        if (form.Equals("shadow", StringComparison.OrdinalIgnoreCase))
        {
            return nameof(PokemonForms.Shadow);
        }

        if (form.Equals("alolan", StringComparison.OrdinalIgnoreCase)
            || form.Equals("alola", StringComparison.OrdinalIgnoreCase))
        {
            return nameof(PokemonForms.Alola);
        }

        if (form.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return nameof(PokemonForms.Normal);
        }

        return form;
    }

    internal static string? GetString(string[] row, IReadOnlyDictionary<string, int> header, string column)
    {
        if (!TryGetField(row, header, column, out var value))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static int? GetInt(
        string[] row,
        IReadOnlyDictionary<string, int> header,
        string column,
        int rowNumber,
        ICollection<CsvValidationFailure> failures)
    {
        if (!TryGetField(row, header, column, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        failures.Add(new CsvValidationFailure(rowNumber, column, value, "Expected an integer value."));
        return null;
    }

    private static decimal? GetDecimal(
        string[] row,
        IReadOnlyDictionary<string, int> header,
        string column,
        int rowNumber,
        ICollection<CsvValidationFailure> failures)
    {
        if (!TryGetField(row, header, column, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Replace("%", string.Empty, StringComparison.Ordinal).Trim();

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        failures.Add(new CsvValidationFailure(rowNumber, column, value, "Expected a decimal value."));
        return null;
    }

    private static DateTime? GetDateTime(
        string[] row,
        IReadOnlyDictionary<string, int> header,
        string column,
        int rowNumber,
        ICollection<CsvValidationFailure> failures)
    {
        if (!TryGetField(row, header, column, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm",
            "M/d/yyyy",
            "M/d/yyyy H:mm",
            "M/d/yyyy h:mm:ss tt"
        };

        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fallback))
        {
            return fallback;
        }

        failures.Add(new CsvValidationFailure(rowNumber, column, value, "Expected a valid date/time value."));
        return null;
    }

    internal static bool TryGetField(string[] row, IReadOnlyDictionary<string, int> header, string column, out string value)
    {
        value = string.Empty;

        if (!header.TryGetValue(column, out var index))
        {
            return false;
        }

        if (index < 0 || index >= row.Length)
        {
            return false;
        }

        value = row[index];
        return true;
    }
}
