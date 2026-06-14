using System.Text.Json;

namespace TeamBuilder.Data.Responses;

public sealed record PogoApiResponse
{
    public required string Endpoint { get; init; }
    public required bool IsSuccess { get; init; }
    public int? StatusCode { get; init; }
    public string? Error { get; init; }
    public string? RawJson { get; init; }
    public JsonElement? Data { get; init; }

    public static PogoApiResponse Success(string endpoint, JsonElement data, string rawJson, int? statusCode) => new()
    {
        Endpoint = endpoint,
        IsSuccess = true,
        Data = data,
        RawJson = rawJson,
        StatusCode = statusCode
    };

    public static PogoApiResponse Fail(string endpoint, string error, int? statusCode = null, string? rawJson = null) => new()
    {
        Endpoint = endpoint,
        IsSuccess = false,
        Error = error,
        StatusCode = statusCode,
        RawJson = rawJson
    };
}