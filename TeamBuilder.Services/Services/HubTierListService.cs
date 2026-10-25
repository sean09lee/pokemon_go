using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.Services;

public sealed class HubTierListService : IHubTierListService
{
    private static readonly string[] AttackerTypes =
    [
        "normal", "fire", "water", "electric", "grass", "ice", "fighting", "poison",
        "ground", "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark",
        "steel", "fairy"
    ];

    private readonly IHubApiService _hubApiService;

    public HubTierListService(IHubApiService hubApiService)
    {
        _hubApiService = hubApiService;
    }

    public async Task<(HubRankingResponse Response, List<HubTierRow> Rows)> CrawlAllTypesAsync(
        int limitPerType = 200,
        Func<string, int, int, Task>? reportProgressAsync = null,
        CancellationToken cancellationToken = default)
    {
        var rows = new List<HubTierRow>();
        var tierMap = new Dictionary<string, IList<HubRankedPokemon>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < AttackerTypes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attackerType = AttackerTypes[i];

            using var perTypeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perTypeCts.CancelAfter(TimeSpan.FromSeconds(120));

            var tierList = await _hubApiService.CrawlBestPerTypeAsync(attackerType, limitPerType, perTypeCts.Token);
            tierMap[attackerType] = tierList;
            AddTierRows(rows, attackerType, tierList);

            if (reportProgressAsync is not null)
            {
                await reportProgressAsync(attackerType, i + 1, AttackerTypes.Length);
            }
        }

        SortRows(rows);

        var response = BuildResponse(key => tierMap.GetValueOrDefault(key) ?? []);
        return (response, rows);
    }

    public HubRankingResponse? BuildFromCachedRows(IReadOnlyCollection<HubTierRow> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        IList<HubRankedPokemon> ForType(string typeKey)
            => rows
                .Where(x => string.Equals(x.AttackerType, typeKey, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Pokemon)
                .ToList();

        return BuildResponse(ForType);
    }

    private static void AddTierRows(List<HubTierRow> rows, string attackerType, IEnumerable<HubRankedPokemon> tierList)
    {
        foreach (var pokemon in tierList)
        {
            rows.Add(new HubTierRow
            {
                AttackerType = attackerType,
                Pokemon = pokemon
            });
        }
    }

    private static void SortRows(List<HubTierRow> rows)
    {
        rows.Sort((a, b) =>
        {
            var typeCompare = string.Compare(a.AttackerType, b.AttackerType, StringComparison.OrdinalIgnoreCase);
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            return (a.Pokemon.Rank ?? int.MaxValue).CompareTo(b.Pokemon.Rank ?? int.MaxValue);
        });
    }

    private static HubRankingResponse BuildResponse(Func<string, IList<HubRankedPokemon>> forType)
    {
        return new HubRankingResponse
        {
            NormalTierList = forType("normal"),
            FireTierList = forType("fire"),
            WaterTierList = forType("water"),
            ElectricTierList = forType("electric"),
            GrassTierList = forType("grass"),
            IceTierList = forType("ice"),
            FightingTierList = forType("fighting"),
            PoisonTierList = forType("poison"),
            GroundTierList = forType("ground"),
            FlyingTierList = forType("flying"),
            PsychicTierList = forType("psychic"),
            BugTierList = forType("bug"),
            RockTierList = forType("rock"),
            GhostTierList = forType("ghost"),
            DragonTierList = forType("dragon"),
            DarkTierList = forType("dark"),
            SteelTierList = forType("steel"),
            FairyTierList = forType("fairy")
        };
    }
}
