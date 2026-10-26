using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.Contracts;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

/// <summary>
/// This service hits the pokemon go hub api to get tier list data
/// </summary>
public class HubApiService(ICacheService cacheService, ILogger<HubApiService>? logger = null) : IHubApiService
{
  private readonly ICacheService _cacheService = cacheService;
  private static readonly TimeSpan DefaultHubTimeout = TimeSpan.FromSeconds(90);

  public async Task<IList<HubRankedPokemon>?> TryGetCachedBestPerTypeAsync(string attackerType, int limit = 200, CancellationToken cancellationToken = default)
  {
    attackerType = attackerType.Trim().ToLowerInvariant();
    var cacheKey = GetCacheKey(attackerType, limit);
    var cached = await _cacheService.TryGetAsync<List<HubRankedPokemon>>(cacheKey, cancellationToken);
    return cached is not null && cached.Count > 0 ? cached : null;
  }

  private static string GetCacheKey(string attackerType, int limit) => $"hub-best-per-type-{attackerType}-{limit}";

  public async Task<IList<HubRankedPokemon>> CrawlBestPerTypeAsync(string attackerType, int limit = 200, bool forceRefresh = false, CancellationToken cancellationToken = default)
  {
    attackerType = attackerType.Trim().ToLowerInvariant();
    using var crawlScope = logger?.BeginScope(new Dictionary<string, object>
    {
      ["AttackerType"] = attackerType,
      ["ProcessId"] = Environment.ProcessId,
      ["InstanceId"] = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName
    });
    logger?.LogInformation("Hub crawl requested for {AttackerType}, limit {RowLimit}, forceRefresh {ForceRefresh}", attackerType, limit, forceRefresh);
    var cacheKey = GetCacheKey(attackerType, limit);

    if (!forceRefresh)
    {
      var cached = await _cacheService.TryGetAsync<List<HubRankedPokemon>>(cacheKey, cancellationToken);
      if (cached is not null && cached.Count > 0)
      {
        logger?.LogInformation("Hub cache hit for {AttackerType}: {RowCount} rows", attackerType, cached.Count);
        return cached;
      }
    }

    var url = $"https://db.pokemongohub.net/pokemon-list/best-per-type/{attackerType}";

    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(DefaultHubTimeout);
    var elapsed = Stopwatch.StartNew();
    var stage = "starting Playwright";
    int? httpStatus = null;
    void LogStage()
    {
      logger?.LogInformation("Hub stage {CrawlStage} at {ElapsedMs} ms; web process working set {WorkingSetMB} MB; managed heap {ManagedHeapMB} MB",
        stage, elapsed.ElapsedMilliseconds, Environment.WorkingSet / (1024 * 1024), GC.GetTotalMemory(false) / (1024 * 1024));
    }

    try
    {
      LogStage();
      using var playwright = await Playwright.CreateAsync();
      stage = "launching Chromium";
      LogStage();
      await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
      {
        Headless = true,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });

      stage = "creating the browser context";
      LogStage();
      var context = await browser.NewContextAsync(new BrowserNewContextOptions
      {
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36"
      });

      context.SetDefaultTimeout((float)DefaultHubTimeout.TotalMilliseconds);
      var page = await context.NewPageAsync();
      page.Crash += (_, _) => logger?.LogError("Hub Chromium page crashed during {CrawlStage} after {ElapsedMs} ms", stage, elapsed.ElapsedMilliseconds);
      page.PageError += (_, error) => logger?.LogWarning("Hub page script error during {CrawlStage}: {PageError}", stage, error);
      stage = "loading the Hub page";
      LogStage();
      var response = await page.GotoAsync(url, new PageGotoOptions
      {
        WaitUntil = WaitUntilState.DOMContentLoaded,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });
      httpStatus = response?.Status;
      logger?.LogInformation("Hub navigation returned HTTP {HttpStatus} after {ElapsedMs} ms", httpStatus, elapsed.ElapsedMilliseconds);
      if (httpStatus >= 400) {
        throw new InvalidOperationException($"Hub returned HTTP {httpStatus} for attacker type '{attackerType}'.");
      }
      stage = "waiting for ranking rows";
      LogStage();
      await page.Locator("table tbody tr a[href*='/pokemon/']").First.WaitForAsync(new LocatorWaitForOptions
      {
        State = WaitForSelectorState.Visible,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });

      timeoutCts.Token.ThrowIfCancellationRequested();
      stage = "setting the row limit";
      LogStage();
      await HubHelpers.SetRowsLimitAsync(page, limit);
      await page.WaitForTimeoutAsync(1200);

      timeoutCts.Token.ThrowIfCancellationRequested();
      stage = "extracting ranking rows";
      LogStage();
      var pokemon = await HubHelpers.ExtractRankedPokemonAsync(page, limit, timeoutCts.Token);
      logger?.LogInformation("Hub extracted {RowCount} rows after {ElapsedMs} ms", pokemon.Count, elapsed.ElapsedMilliseconds);

      var ranked = pokemon
        .GroupBy(x => $"{x.PokemonNumber}|{x.Name}|{x.Form}")
        .Select(x => x.First())
        .Take(limit)
        .ToList();

      stage = "saving the ranking cache";
      LogStage();
      await _cacheService.SaveAsync(cacheKey, ranked, cancellationToken);

      logger?.LogInformation("Hub crawl completed with {RowCount} rows in {ElapsedMs} ms", ranked.Count, elapsed.ElapsedMilliseconds);
      return ranked;
    }
    catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or PlaywrightException or InvalidOperationException)
    {
      if (cancellationToken.IsCancellationRequested) {
        logger?.LogWarning("Hub crawl canceled during {CrawlStage} after {ElapsedMs} ms", stage, elapsed.ElapsedMilliseconds);
        throw new OperationCanceledException("Hub crawl was canceled.", ex, cancellationToken);
      }
      var timedOut = timeoutCts.IsCancellationRequested || ex is TimeoutException;
      var message = $"Hub crawl {(timedOut ? "timed out" : "failed")} for attacker type '{attackerType}' during {stage} after {elapsed.Elapsed.TotalSeconds:F1}s (HTTP {httpStatus?.ToString() ?? "unknown"}).";
      logger?.LogError(ex, "{Message} URL: {HubUrl}; stage {CrawlStage}; elapsed {ElapsedMs} ms; HTTP {HttpStatus}; web process working set {WorkingSetMB} MB",
        message, url, stage, elapsed.ElapsedMilliseconds, httpStatus, Environment.WorkingSet / (1024 * 1024));
      if (timedOut) {
        throw new TimeoutException(message, ex);
      }
      throw new InvalidOperationException($"{message} {ex.Message}", ex);
    }
  }
}
