using Microsoft.Playwright;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.Helpers;

namespace TeamBuilder.Services.Services;

/// <summary>
/// This service hits the pokemon go hub api to get tier list data
/// </summary>
public class HubApiService
{
  public static async Task<IList<HubRankedPokemon>> CrawlBestPerTypeAsync(string attackerType, int limit = 200, CancellationToken cancellationToken = default)
  {
    attackerType = attackerType.Trim().ToLowerInvariant();
    var url = $"https://db.pokemongohub.net/pokemon-list/best-per-type/{attackerType}";

    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
      Headless = true
    });

    var context = await browser.NewContextAsync(new BrowserNewContextOptions
    {
      UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36"
    });

    var page = await context.NewPageAsync();
    await page.GotoAsync(url, new PageGotoOptions
    {
      WaitUntil = WaitUntilState.NetworkIdle,
      Timeout = 90000
    });

    await HubHelpers.SetRowsLimitAsync(page, limit);
    await page.WaitForTimeoutAsync(1200);

    var pokemon = await HubHelpers.ExtractRankedPokemonAsync(page, limit, cancellationToken);

    return pokemon
      .GroupBy(x => $"{x.PokemonNumber}|{x.Name}|{x.Form}")
      .Select(x => x.First())
      .Take(limit)
      .ToList();
  }
}
