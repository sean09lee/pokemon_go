# Creates/updates Linux container hosting without replacing existing Windows resources.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'test', 'prod')]
    [string]$Environment
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/_common.ps1"

$names = Get-InfraNames -Environment $Environment
$rg = $names.ResourceGroup
$planRg = $names.PlanResourceGroup
$plan = $names.Plan
$webApp = $names.WebApp
$registry = $names.Registry

# Hardcoded desired state
$desiredPlanSku = 'B1'
$desiredMinTls = '1.2'

# --- App Service plan ---
Write-Host "Checking App Service plan '$plan'..."
$existingPlan = Get-AzResourceOrNull "az appservice plan show --name $plan --resource-group $planRg"
$existingApp = Get-AzResourceOrNull "az webapp show --name $webApp --resource-group $rg"
foreach ($resource in @(@{ Name = $plan; Value = $existingPlan }, @{ Name = $webApp; Value = $existingApp })) {
    if ($null -eq $resource.Value) { continue }
    if ($resource.Value.reserved -eq $true -or $resource.Value.kind -match '(^|,)\s*linux\s*(,|$)') { continue }
    if ($resource.Value.reserved -eq $false) {
        throw "Resource '$($resource.Name)' is Windows. Azure cannot convert these resources to Linux. Back up and explicitly migrate the Windows resource before rerunning infrastructure. This script will not delete it. Retain shared services."
    }
    throw "Unable to determine the operating system of '$($resource.Name)' from Azure metadata. A missing reserved property does not mean Windows. Verify the resource configuration before rerunning; no resources have been changed."
}

if ($null -ne $existingPlan -and
    ($existingPlan.location -replace '\s', '') -ine ($names.Location -replace '\s', '')) {
    throw "Shared plan '$plan' must be in '$($names.Location)' so all applications use the same region."
}

$sharedGroup = Get-AzResourceOrNull "az group show --name $planRg"
if ($null -eq $sharedGroup) {
    az group create --name $planRg --location $names.Location --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create shared hosting resource group '$planRg'." }
}

$registryProvider = 'Microsoft.ContainerRegistry'
$registrationState = az provider show --namespace $registryProvider --query registrationState -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or $registrationState -ne 'Registered') {
    Write-Host "Registering resource provider '$registryProvider'..."
    az provider register --namespace $registryProvider --wait | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to register '$registryProvider'. The service connection needs Microsoft.ContainerRegistry/register/action at subscription scope, or a subscription administrator must run: az provider register --namespace $registryProvider --wait"
    }
}

$existingRegistry = Get-AzResourceOrNull "az acr show --name $registry --resource-group $rg"
if ($null -eq $existingRegistry) {
    az acr create --name $registry --resource-group $rg --location $names.Location --sku Basic --admin-enabled false --role-assignment-mode rbac --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create container registry '$registry'." }
}

if ($null -eq $existingPlan) {
    Write-Host "App Service plan '$plan' does not exist. Creating..."
    $maxAttempts = 5
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $createOutput = az appservice plan create --name $plan --resource-group $planRg --location $names.Location --sku $desiredPlanSku --is-linux --tags $script:TagArguments 2>&1
        if ($LASTEXITCODE -eq 0) { break }
        if ($attempt -eq $maxAttempts) { throw "Failed to create App Service plan '$plan' after $maxAttempts attempts. Last error: $($createOutput | Out-String)" }

        $isThrottled = ($createOutput | Out-String) -match 'throttled'
        $delay = if ($isThrottled) { 60 * [Math]::Pow(2, $attempt - 1) } else { 30 * $attempt }
        $delay = [Math]::Min($delay, 300)
        $reason = if ($isThrottled) { 'throttled' } else { 'failed' }
        Write-Host "Plan creation $reason (attempt $attempt/$maxAttempts). Retrying in $delay seconds..."
        Start-Sleep -Seconds $delay
    }
    Write-Host "Created App Service plan '$plan'."

}
else {
    Write-Host "App Service plan '$plan' exists. Comparing properties..."
    if ($existingPlan.sku.name -ne $desiredPlanSku) {
        Write-Host "SKU drift detected ('$($existingPlan.sku.name)' vs '$desiredPlanSku'). Updating..."
        az appservice plan update --name $plan --resource-group $planRg --sku $desiredPlanSku | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to update App Service plan '$plan'." }
        Write-Host "Updated App Service plan '$plan' SKU."
    }
    else {
        Write-Host "No drift detected on App Service plan '$plan'."
    }
}

$planId = az appservice plan show --name $plan --resource-group $planRg --query id -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($planId)) {
    throw "Failed to resolve shared plan '$plan'."
}

# --- Web app ---
Write-Host "Checking web app '$webApp'..."
$existingApp = Get-AzResourceOrNull "az webapp show --name $webApp --resource-group $rg"

if ($null -eq $existingApp) {
    Write-Host "Web app '$webApp' does not exist. Creating..."

    az webapp create --name $webApp --resource-group $rg --plan $planId --deployment-container-image-name mcr.microsoft.com/dotnet/samples:aspnetapp --https-only true --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create web app '$webApp'." }
    az webapp config set --name $webApp --resource-group $rg --min-tls-version $desiredMinTls | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to configure web app '$webApp'." }
    Write-Host "Created web app '$webApp'."
}
else {
    Write-Host "Web app '$webApp' exists. Comparing properties..."

    $currentPlanId = $existingApp.appServicePlanId
    if ([string]::IsNullOrWhiteSpace($currentPlanId)) { $currentPlanId = $existingApp.serverFarmId }
    if ($currentPlanId -ine $planId) {
        throw "Web app '$webApp' is not attached to shared plan '$plan'. Explicitly migrate or recreate this app on the shared plan before rerunning. Retire unused environment-specific plans separately to avoid ongoing charges."
    }

    if ($existingApp.httpsOnly -ne $true) {
        Write-Host "HTTPS-only drift detected. Enabling..."
        az webapp update --name $webApp --resource-group $rg --set httpsOnly=true | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to enable HTTPS-only on '$webApp'." }
    }

    $config = az webapp config show --name $webApp --resource-group $rg | ConvertFrom-Json
    $needsConfigUpdate = $false
    if ($config.linuxFxVersion -notlike 'DOCKER|*') {
        throw "Web app '$webApp' is not configured for a container. Review its hosting configuration before deploying."
    }
    if ($config.minTlsVersion -ne $desiredMinTls) {
        Write-Host "Minimum TLS drift detected ('$($config.minTlsVersion)' vs '$desiredMinTls')."
        $needsConfigUpdate = $true
    }

    if ($needsConfigUpdate) {
        az webapp config set --name $webApp --resource-group $rg --min-tls-version $desiredMinTls | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to update config on web app '$webApp'." }
        Write-Host "Updated web app '$webApp' configuration."
    }
    else {
        Write-Host "No drift detected on web app '$webApp'."
    }
}

# --- System-assigned managed identity ---
# Grants the web app its own Entra identity so Key Vault/App Configuration RBAC roles
# can be scoped to the app itself rather than relying on connection strings/secrets.
Write-Host "Checking system-assigned managed identity on web app '$webApp'..."
$identity = az webapp identity show --name $webApp --resource-group $rg | ConvertFrom-Json
if ($null -eq $identity -or [string]::IsNullOrWhiteSpace($identity.principalId)) {
    Write-Host "Assigning system-assigned managed identity to web app '$webApp'..."
    az webapp identity assign --name $webApp --resource-group $rg | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to assign managed identity to web app '$webApp'." }
    Write-Host "Assigned system-assigned managed identity to web app '$webApp'."
}
else {
    Write-Host "Web app '$webApp' already has a system-assigned managed identity."
}

$principalId = Get-WebAppPrincipalId -WebAppName $webApp -ResourceGroup $rg
if ([string]::IsNullOrWhiteSpace($principalId)) { throw "Managed identity for '$webApp' is unavailable." }
$registryId = az acr show --name $registry --resource-group $rg --query id -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($registryId)) { throw "Failed to resolve registry '$registry'." }
Ensure-RoleAssignment -Scope $registryId -RoleName 'AcrPull' -AssigneeObjectId $principalId -PrincipalType ServicePrincipal -AssigneeDisplayName $webApp

az webapp config set --name $webApp --resource-group $rg --always-on true --web-sockets-enabled true | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to configure container hosting on '$webApp'." }
$appId = az webapp show --name $webApp --resource-group $rg --query id -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($appId)) { throw "Failed to resolve web app '$webApp'." }
az resource update --ids "$appId/config/web" --set properties.acrUseManagedIdentityCreds=true properties.healthCheckPath=/health/live --output none
if ($LASTEXITCODE -ne 0) { throw "Failed to configure registry authentication for '$webApp'." }
az webapp config appsettings set --name $webApp --resource-group $rg --settings WEBSITES_PORT=8080 ASPNETCORE_HTTP_PORTS=8080 WEBSITES_ENABLE_APP_SERVICE_STORAGE=false PLAYWRIGHT_BROWSERS_PATH=/ms-playwright --output none
if ($LASTEXITCODE -ne 0) { throw "Failed to configure container settings for '$webApp'." }
az webapp log config --name $webApp --resource-group $rg --docker-container-logging filesystem --output none
if ($LASTEXITCODE -ne 0) { throw "Failed to enable container logs for '$webApp'." }
