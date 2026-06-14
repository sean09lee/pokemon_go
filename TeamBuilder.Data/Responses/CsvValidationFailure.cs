namespace TeamBuilder.Data.Responses;

public sealed record CsvValidationFailure(int RowNumber, string ColumnName, string? RawValue, string Message);
