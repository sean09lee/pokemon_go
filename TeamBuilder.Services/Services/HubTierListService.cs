using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<HubTierListService>? _logger;

    public HubTierListService(IHubApiService hubApiService, ILogger<HubTierListService>? logger = null)
    {
        _hubApiService = hubApiService;
        _logger = logger;
    }

    public async Task<List<HubTierRow>> LoadAllTypesFromCacheAsync(
        int limitPerType = 200,
        CancellationToken cancellationToken = default)
    {
        var rows = new List<HubTierRow>();

        foreach (var attackerType in AttackerTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cached = await _hubApiService.TryGetCachedBestPerTypeAsync(attackerType, limitPerType, cancellationToken);
            if (cached is not null && cached.Count > 0)
            {
                AddTierRows(rows, attackerType, cached);
            }
        }

        SortRows(rows);
        return rows;
    }

    public async Task<(HubRankingResponse Response, List<HubTierRow> Rows)> CrawlAllTypesAsync(
        int limitPerType = 200,
        Func<string, int, int, Task>? reportProgressAsync = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        using var activity = new Activity("HubTierListCrawl").Start();
        var crawlId = Guid.NewGuid().ToString("N");
        using var crawlScope = _logger?.BeginScope(new Dictionary<string, object>
        {
            ["CrawlId"] = crawlId,
            ["ProcessId"] = Environment.ProcessId,
            ["InstanceId"] = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName
        });
        var elapsed = Stopwatch.StartNew();
        _logger?.LogInformation("Hub tier-list crawl {CrawlId} started: {TypeCount} types, {RowLimit} rows per type", crawlId, AttackerTypes.Length, limitPerType);
        var rows = new List<HubTierRow>();
        var tierMap = new Dictionary<string, IList<HubRankedPokemon>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < AttackerTypes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attackerType = AttackerTypes[i];
            _logger?.LogInformation("Hub type {AttackerType} started ({TypeIndex}/{TypeCount}) after {ElapsedMs} ms", attackerType, i + 1, AttackerTypes.Length, elapsed.ElapsedMilliseconds);

            using var perTypeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perTypeCts.CancelAfter(TimeSpan.FromSeconds(120));

            IList<HubRankedPokemon> tierList;
            try
            {
                tierList = await _hubApiService.CrawlBestPerTypeAsync(attackerType, limitPerType, forceRefresh, perTypeCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Hub tier-list crawl {CrawlId} canceled at {AttackerType} after {ElapsedMs} ms; caller canceled: {CallerCanceled}", crawlId, attackerType, elapsed.ElapsedMilliseconds, cancellationToken.IsCancellationRequested);
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Hub tier-list crawl {CrawlId} failed at {AttackerType} after {ElapsedMs} ms", crawlId, attackerType, elapsed.ElapsedMilliseconds);
                throw;
            }
            tierMap[attackerType] = tierList;
            AddTierRows(rows, attackerType, tierList);
            _logger?.LogInformation("Hub type {AttackerType} completed with {RowCount} rows after {ElapsedMs} ms", attackerType, tierList.Count, elapsed.ElapsedMilliseconds);

            if (reportProgressAsync is not null)
            {
                _logger?.LogInformation("Hub reporting progress for {AttackerType}", attackerType);
                await reportProgressAsync(attackerType, i + 1, AttackerTypes.Length);
                _logger?.LogInformation("Hub progress reported for {AttackerType}", attackerType);
            }
        }

        SortRows(rows);

        var response = BuildResponse(key => tierMap.GetValueOrDefault(key) ?? []);
        _logger?.LogInformation("Hub tier-list crawl {CrawlId} completed with {RowCount} rows in {ElapsedMs} ms", crawlId, rows.Count, elapsed.ElapsedMilliseconds);
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
