# Pokemon GO Team Builder

Blazor web app that helps you build raid teams from your own Pokémon collection. This site is available at https://pokemonroster.com.

## What this site does

- Uploads a **Poke Genie CSV export** of your Pokémon.
- Crawls Pokémon GO Hub attacker tier lists for all 18 attacker types.
- Pulls evolution data from `https://pogoapi.net/`.
- Builds a **Top 6 owned raid team** per type based on tier rank and your owned Pokémon.
- Displays:
  - all crawled tier-list entries,
  - CSV validation warnings,
  - your uploaded Pokémon,
  - ranked raid team tables by type.

## Tech stack

- .NET 10
- ASP.NET Core Blazor (interactive server)
- Microsoft Playwright (for tier-list crawling)

## Run locally

### 1. Prerequisites

- Install **.NET 10 SDK**
- Install **PowerShell** (Windows PowerShell or PowerShell 7)

### 2. Restore and build

From the repository root:

- `dotnet restore`
- `dotnet build .\TeamBuilder\TeamBuilder.csproj`

### 3. Install Playwright browser binaries (first time only)

After a successful build, install Chromium used by the crawler:

- `powershell -ExecutionPolicy Bypass -File .\TeamBuilder\bin\Debug\net10.0\playwright.ps1 install`

If you build in Release, use the corresponding `Release\net10.0` path.

### 4. Run the app

- `dotnet run --project .\TeamBuilder\TeamBuilder.csproj`

Default local URLs (from launch settings):

- `https://localhost:7028`
- `http://localhost:5024`

## How to use

1. Export your Pokémon from Poke Genie as CSV.
2. Open the app and click **Upload CSV**.
3. Wait for tier-list crawling and ranking to complete.
4. Expand the result sections to inspect your teams.

## Pipelines

| Pipeline | Status |
| --- | --- |
| Build | [![Build Status](https://sean09lee.visualstudio.com/Team%20Builder/_apis/build/status%2FBuilds%2FTeamBuilder-Build?branchName=main)](https://sean09lee.visualstudio.com/Team%20Builder/_build/latest?definitionId=18&branchName=main) |
| Release | [![Release Status](https://sean09lee.visualstudio.com/Team%20Builder/_apis/build/status%2FReleases%2FTeamBuilder-Release?branchName=main)](https://sean09lee.visualstudio.com/Team%20Builder/_build/latest?definitionId=19&branchName=main) |
| Infrastructure | [![Infrastructure Status](https://sean09lee.visualstudio.com/Team%20Builder/_apis/build/status%2FInfrastructure%2FTeamBuilder-Infrastructure?branchName=main)](https://sean09lee.visualstudio.com/Team%20Builder/_build/latest?definitionId=17&branchName=main) |

## Notes

- The first crawl can take longer because Playwright starts a headless browser.
- The app requires internet access for Pokémon GO Hub and PoGoAPI requests.
