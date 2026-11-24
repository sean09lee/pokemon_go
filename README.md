# Pokémon GO Team Builder

Blazor web app that helps trainers build raid teams from their own Pokémon collection. See the production site at https://pokemonroster.com.

## How to use

1. Export your Pokémon from Poke Genie as CSV.
2. Open the app and click **Upload CSV**.
3. Wait for tier-list crawling and ranking to complete.
4. Expand the result sections to inspect your teams.

### Additional notes

- The first crawl can take longer because Playwright starts a headless browser.
- The app requires internet access for Pokémon GO Hub and PoGoAPI requests.

## Tech Stack

- .NET 10
- ASP.NET Core Blazor (interactive server)
- Microsoft Playwright (for tier-list crawling)

## Run Locally

### 1. Prerequisites

- Install **.NET 10 SDK**
- Install **PowerShell** (Windows PowerShell or PowerShell 7)
- Install **Playwright** (via `dotnet tool install --global Microsoft.Playwright.CLI`)

### 2. Restore and build

From the repository root:

- `dotnet restore`
- `dotnet build .\TeamBuilder\TeamBuilder.csproj`

### 3. Run the app

- `dotnet run --project .\TeamBuilder\TeamBuilder.csproj`
- Navigate to `https://localhost:7028`

## Pipelines

| Pipeline | Status |
| --- | --- |
| Build | [![Build Status](https://sean09lee.visualstudio.com/Team%20Builder/_apis/build/status%2FBuilds%2FTeamBuilder-Build?branchName=main)](https://sean09lee.visualstudio.com/Team%20Builder/_build/latest?definitionId=18&branchName=main) |
| Release | [![Release Status](https://sean09lee.visualstudio.com/Team%20Builder/_apis/build/status%2FReleases%2FTeamBuilder-Release?branchName=main)](https://sean09lee.visualstudio.com/Team%20Builder/_build/latest?definitionId=19&branchName=main) |
| Infrastructure | [![Infrastructure Status](https://sean09lee.visualstudio.com/Team%20Builder/_apis/build/status%2FInfrastructure%2FTeamBuilder-Infrastructure?branchName=main)](https://sean09lee.visualstudio.com/Team%20Builder/_build/latest?definitionId=17&branchName=main) |

## Agentic AI

Development for this application uses agentic AI to help plan, implement, and verify changes. The agents are:

- **Product Manager**: Breaks requested features into tasks, defines acceptance criteria, and identifies dependencies and risks.
- **Developer**: Implements and verifies focused changes to the application using its existing service-repository architecture.
- **QA**: Validates that the implemented changes meet the acceptance criteria and do not introduce regressions.

## Additional Documentation

- [Agentic AI Orchestration](docs/agentic-ai-orchestration.md)
- [AI Function Calling](docs/ai-function-calling.md)
- [App Config Setup](docs/app-config-setup.md)
- [Linux Container Deployment](docs/linux-container-deployment.md)
- [Pokémon Ranking Logic](docs/pokemon-ranking-logic.md)
