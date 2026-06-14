using TeamBuilder.Data.Models;

namespace TeamBuilder.Data.Responses;

public sealed record HubCountersResponse
{
    public int? StatusCode { get; init; }
    public string? Error { get; init; }
    public string? RawJson { get; init; }
    public List<HubCounterEntry> Entries { get; init; } = [];
}
