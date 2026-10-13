using Microsoft.Playwright;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.Contracts;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

/// <summary>
/// This service hits the pokemon go hub api to get tier list data
/// </summary>
public class HubApiService(ICacheService cacheService) : IHubApiService
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

    try
    {
      using var playwright = await Playwright.CreateAsync();
      await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
      {
        Headless = true,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });

      var context = await browser.NewContextAsync(new BrowserNewContextOptions
      {
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36"
      });

      context.SetDefaultTimeout((float)DefaultHubTimeout.TotalMilliseconds);
      var page = await context.NewPageAsync();
      await page.GotoAsync(url, new PageGotoOptions
      {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = (float)DefaultHubTimeout.TotalMilliseconds
      });

      timeoutCts.Token.ThrowIfCancellationRequested();
      await HubHelpers.SetRowsLimitAsync(page, limit);
      await page.WaitForTimeoutAsync(1200);

      timeoutCts.Token.ThrowIfCancellationRequested();
      var pokemon = await HubHelpers.ExtractRankedPokemonAsync(page, limit, timeoutCts.Token);

      var ranked = pokemon
        .GroupBy(x => $"{x.PokemonNumber}|{x.Name}|{x.Form}")
        .Select(x => x.First())
        .Take(limit)
        .ToList();

      await _cacheService.SaveAsync(cacheKey, ranked, cancellationToken);

      return ranked;
    }
    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
    {
      throw new TimeoutException($"Hub request timed out while processing attacker type '{attackerType}'.");
    }
    catch (PlaywrightException ex)
    {
      throw new InvalidOperationException(
        $"Failed to crawl Hub for attacker type '{attackerType}'. Ensure Playwright Chromium is installed on the server and outbound HTTPS is allowed. {ex.Message}",
        ex);
    }
  }
}
