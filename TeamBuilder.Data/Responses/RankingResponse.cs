using System;
using TeamBuilder.Data.Models;

namespace TeamBuilder.Data.Responses;

public class RankingResponse
{
    public IDictionary<string, RaidTeamVariantResult> RaidTeamsByType { get; init; } =
        new Dictionary<string, RaidTeamVariantResult>(StringComparer.OrdinalIgnoreCase);
}
