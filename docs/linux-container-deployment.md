# Linux Container Deployment

Azure hosting uses one shared Linux B1 App Service plan, `shared-plan-teambuilder<hash>`, in `shared-rg-teambuilder<hash>`. Dev, test, and production web apps all use that plan in the configured `westus2` region, sharing its CPU and memory. Running infrastructure for another environment reuses the same plan rather than creating another billable plan. Web app names remain environment-specific, with no OS segment added.

Each environment retains its own application resource group, private Basic Azure Container Registry (ACR), configuration, identity, and data services. The single B1 plan and each registry are paid resources; sharing the plan does not eliminate registry or other service charges. Dev workloads, browser crawls, and plan scaling affect production capacity too. Do not delete the shared hosting resource group when removing one environment.

The Docker image includes .NET 10, the Linux Playwright driver, matching Chromium headless shell, and its Ubuntu dependencies. The container runs as a non-root user on port 8080. Browser files are installed at build time, not downloaded at application startup.

## Resource Names

Names follow `[env]-[resource]-teambuilder[hash]`. The four-character hash continues to use the existing subscription ID and salt, so the naming change does not change the hash itself. Shared hosting uses `shared` instead of an application environment.

Examples for dev with hash `410c`:

| Resource | Name |
| --- | --- |
| Resource group | `dev-rg-teambuilder410c` |
| Shared hosting resource group | `shared-rg-teambuilder410c` |
| Shared App Service plan | `shared-plan-teambuilder410c` |
| Web app | `dev-web-teambuilder410c` |
| Container registry | `devacrteambuilder410c` |
| Application Insights | `dev-insights-teambuilder410c` |
| Log Analytics | `dev-logs-teambuilder410c` |
| App Configuration | `dev-config-teambuilder410c` |
| Key Vault | `dev-kv-teambuilder410c` |
| SQL server | `dev-sql-teambuilder410c` |
| SQL database | `dev-db-teambuilder410c` |
| SQL firewall rule | `dev-firewall-teambuilder410c` |
| AI Foundry account | `dev-ai-teambuilder410c` |
| AI model deployment | `dev-model-teambuilder410c` |

Azure Container Registry permits only letters and numbers, so its name omits hyphens. Model identifiers, SQL administrator logins, configuration keys, and the external Entra administrator group are not renamed.

Changing these names does not rename or move existing Azure resources. Infrastructure runs target new resource groups and resources, including a new SQL server/database and Key Vault. Migrate database contents, secrets, configuration values, custom domains, and any other required state explicitly before cutover. Old resources remain billable until separately retired. Do not delete the old resource groups as a shortcut for renaming.

## One-Time Migration

1. Back up the existing Windows app configuration, custom-domain/TLS bindings, deployment settings, and any application data. Schedule downtime. Azure cannot convert a Windows plan or web app to Linux in place.
2. Explicitly delete only the Windows web app when ready to migrate. Retire its old environment-specific plan only after checking that no other apps use it. Do not delete the resource group, SQL database, Key Vault, App Configuration, or Application Insights. Reusing the app name after deletion is subject to Azure name availability. Existing Linux apps on other plans also require an explicit migration or recreation; the script stops rather than silently leaving a separately billed plan in use.
3. Run the infrastructure pipeline for each environment. The first run creates the shared Linux B1 plan; later environments reuse it by full resource ID across resource groups. Each run creates its environment's app and ACR Basic registry and grants the new app identity registry pull access plus the existing shared-service permissions. The initial app is a Microsoft sample container until the first application release. Existing unused plans are not deleted automatically.
4. Ensure the Azure DevOps `teambuilder_connection` service principal can push to the registry (for example, Contributor or AcrPush access). Infrastructure also requires permission to create managed-identity role assignments, as before. Registry admin credentials are disabled.
5. Run a new application build, then release its container artifact to the environment. Old ZIP artifacts are not compatible with the container release pipeline.
6. Verify `/health/live`, `/health/ready`, and a Hub crawl. Restore custom domains and TLS bindings on the recreated app. Do not delete shared services during cleanup.

## Build And Release

The build pipeline uses an Ubuntu agent, runs the .NET tests, builds the Docker image, and launches Chromium inside that image before exporting `image.tar`. The image tag is the build ID. Release loads the artifact, pushes the same image to the environment registry, and selects that tag on App Service. Managed identity authenticates image pulls; no registry password is stored on the app.

App Configuration connection strings and labels are supplied through `ConnectionStrings__AppConfiguration` and `ConnectionStrings__Label` app settings. Do not commit environment secrets to application settings files or bake them into images. Uploaded collection state remains scoped application state and is not durable across container restarts.

For local validation with Docker installed:

```powershell
docker build -t teambuilder:local .
docker run --rm --ipc=host --entrypoint /app/.playwright/node/linux-x64/node teambuilder:local -e "const { chromium } = require('/app/.playwright/package'); (async () => { const browser = await chromium.launch(); await browser.close(); })().catch(error => { console.error(error); process.exit(1); });"
pwsh -NoProfile -File infrastructure/tests/ContainerDeployment.Tests.ps1
```

For Azure startup failures, use App Service Log stream or `az webapp log tail --name <app-name> --resource-group <resource-group>`. Container console logging is enabled by infrastructure. Deployment no longer uses Kudu ZIP extraction.
