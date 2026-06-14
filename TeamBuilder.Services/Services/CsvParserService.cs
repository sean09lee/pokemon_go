using Microsoft.VisualBasic.FileIO;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

public class CsvParserService
{
  public static async Task<CsvParseResult> ParsePokemonCsvAsync(Stream csvStream, CancellationToken cancellationToken = default)
  {
    var result = new CsvParseResult();

    if (csvStream is null)
    {
      return result with { FatalError = "CSV stream was not provided." };
    }

    try
    {
      await using var bufferedStream = new MemoryStream();
      await csvStream.CopyToAsync(bufferedStream, cancellationToken);
      bufferedStream.Position = 0;

      using var reader = new StreamReader(bufferedStream, leaveOpen: false);
      using var parser = new TextFieldParser(reader)
      {
        TextFieldType = FieldType.Delimited,
        HasFieldsEnclosedInQuotes = true,
        TrimWhiteSpace = true
      };

      parser.SetDelimiters(",");

      if (parser.EndOfData)
      {
        return result with { FatalError = "CSV file is empty." };
      }

      var headers = parser.ReadFields();
      if (headers is null || headers.Length == 0)
      {
        return result with { FatalError = "CSV headers were not found." };
      }

      var indexByHeader = CsvHelpers.BuildHeaderIndex(headers);
      var rowNumber = 1;

      while (!parser.EndOfData)
      {
        rowNumber++;
        var fields = parser.ReadFields();
        if (fields is null || fields.Length == 0)
        {
          continue;
        }

        result.Pokemon.Add(CsvHelpers.ParsePokemon(fields, indexByHeader, rowNumber, result.ValidationFailures));
      }

      return result;
    }
    catch (MalformedLineException ex)
    {
      return result with { FatalError = $"Malformed CSV at line {ex.LineNumber}: {ex.Message}" };
    }
    catch (Exception ex)
    {
      return result with { FatalError = $"Failed to parse CSV: {ex.Message}" };
    }
  }
}
