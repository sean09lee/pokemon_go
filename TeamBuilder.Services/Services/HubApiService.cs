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

  public async Task<IList<HubRankedPokemon>> CrawlBestPerTypeAsync(string attackerType, int limit = 200, CancellationToken cancellationToken = default)
  {
    attackerType = attackerType.Trim().ToLowerInvariant();
    var cacheKey = $"hub-best-per-type-{attackerType}-{limit}";

    var cached = await _cacheService.TryGetAsync<List<HubRankedPokemon>>(cacheKey, cancellationToken);
    if (cached is not null && cached.Count > 0)
    {
      return cached;
    }

    var url = $"https://db.pokemongohub.net/pokemon-list/best-per-type/{attackerType}";

    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(DefaultHubTimeout);
    var elapsed = Stopwatch.StartNew();
    var stage = "starting Playwright";
    int? httpStatus = null;

    try
    {
      using var playwright = await Playwright.CreateAsync();
      stage = "launching Chromium";
      await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
      {
        Headless = true,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });

      stage = "creating the browser context";
      var context = await browser.NewContextAsync(new BrowserNewContextOptions
      {
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36"
      });

      context.SetDefaultTimeout((float)DefaultHubTimeout.TotalMilliseconds);
      var page = await context.NewPageAsync();
      stage = "loading the Hub page";
      var response = await page.GotoAsync(url, new PageGotoOptions
      {
        WaitUntil = WaitUntilState.DOMContentLoaded,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });
      httpStatus = response?.Status;
      if (httpStatus >= 400) {
        throw new InvalidOperationException($"Hub returned HTTP {httpStatus} for attacker type '{attackerType}'.");
      }
      stage = "waiting for ranking rows";
      await page.Locator("table tbody tr a[href*='/pokemon/']").First.WaitForAsync(new LocatorWaitForOptions
      {
        State = WaitForSelectorState.Visible,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });

      timeoutCts.Token.ThrowIfCancellationRequested();
      stage = "setting the row limit";
      await HubHelpers.SetRowsLimitAsync(page, limit);
      await page.WaitForTimeoutAsync(1200);

      timeoutCts.Token.ThrowIfCancellationRequested();
      stage = "extracting ranking rows";
      var pokemon = await HubHelpers.ExtractRankedPokemonAsync(page, limit, timeoutCts.Token);

      var ranked = pokemon
        .GroupBy(x => $"{x.PokemonNumber}|{x.Name}|{x.Form}")
        .Select(x => x.First())
        .Take(limit)
        .ToList();

      stage = "saving the ranking cache";
      await _cacheService.SaveAsync(cacheKey, ranked, cancellationToken);

      return ranked;
    }
    catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or PlaywrightException or InvalidOperationException)
    {
      if (cancellationToken.IsCancellationRequested) {
        throw new OperationCanceledException("Hub crawl was canceled.", ex, cancellationToken);
      }
      var timedOut = timeoutCts.IsCancellationRequested || ex is TimeoutException;
      var message = $"Hub crawl {(timedOut ? "timed out" : "failed")} for attacker type '{attackerType}' during {stage} after {elapsed.Elapsed.TotalSeconds:F1}s (HTTP {httpStatus?.ToString() ?? "unknown"}).";
      logger?.LogError(ex, "{Message} URL: {HubUrl}", message, url);
      if (timedOut) {
        throw new TimeoutException(message, ex);
      }
      throw new InvalidOperationException($"{message} {ex.Message}", ex);
    }
  }
}
