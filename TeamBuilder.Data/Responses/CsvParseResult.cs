using TeamBuilder.Data.Models;

namespace TeamBuilder.Data.Responses;

public sealed record CsvParseResult
{
    public List<Pokemon> Pokemon { get; init; } = [];
    public List<CsvValidationFailure> ValidationFailures { get; init; } = [];
    public string? FatalError { get; init; }
    public bool HasValidationFailures => ValidationFailures.Count > 0;
}
