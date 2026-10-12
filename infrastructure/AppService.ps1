# Creates/updates the App Service plan and the Blazor web app.
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
$plan = $names.Plan
$webApp = $names.WebApp

# Hardcoded desired state
$desiredPlanSku = 'B1'
$desiredFramework = 'v10.0'
$desiredMinTls = '1.2'

# --- App Service plan ---
Write-Host "Checking App Service plan '$plan'..."
$existingPlan = Get-AzResourceOrNull "az appservice plan show --name $plan --resource-group $rg"

if ($null -eq $existingPlan) {
    Write-Host "App Service plan '$plan' does not exist. Creating..."
    az appservice plan create --name $plan --resource-group $rg --location $names.Location --sku $desiredPlanSku --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create App Service plan '$plan'." }
    Write-Host "Created App Service plan '$plan'."
}
else {
    Write-Host "App Service plan '$plan' exists. Comparing properties..."
    if ($existingPlan.sku.name -ne $desiredPlanSku) {
        Write-Host "SKU drift detected ('$($existingPlan.sku.name)' vs '$desiredPlanSku'). Updating..."
        az appservice plan update --name $plan --resource-group $rg --sku $desiredPlanSku | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to update App Service plan '$plan'." }
        Write-Host "Updated App Service plan '$plan' SKU."
    }
    else {
        Write-Host "No drift detected on App Service plan '$plan'."
    }
}

# --- Web app ---
Write-Host "Checking web app '$webApp'..."
$existingApp = Get-AzResourceOrNull "az webapp show --name $webApp --resource-group $rg"

if ($null -eq $existingApp) {
    Write-Host "Web app '$webApp' does not exist. Creating..."
    az webapp create --name $webApp --resource-group $rg --plan $plan --https-only true --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create web app '$webApp'." }
    az webapp config set --name $webApp --resource-group $rg --net-framework-version $desiredFramework --min-tls-version $desiredMinTls | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to configure web app '$webApp'." }
    Write-Host "Created web app '$webApp'."
    return
}

Write-Host "Web app '$webApp' exists. Comparing properties..."

if ($existingApp.httpsOnly -ne $true) {
    Write-Host "HTTPS-only drift detected. Enabling..."
    az webapp update --name $webApp --resource-group $rg --set httpsOnly=true | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to enable HTTPS-only on '$webApp'." }
}

$config = az webapp config show --name $webApp --resource-group $rg | ConvertFrom-Json
$needsConfigUpdate = $false
if ($config.netFrameworkVersion -ne $desiredFramework) {
    Write-Host ".NET framework drift detected ('$($config.netFrameworkVersion)' vs '$desiredFramework')."
    $needsConfigUpdate = $true
}
if ($config.minTlsVersion -ne $desiredMinTls) {
    Write-Host "Minimum TLS drift detected ('$($config.minTlsVersion)' vs '$desiredMinTls')."
    $needsConfigUpdate = $true
}

if ($needsConfigUpdate) {
    az webapp config set --name $webApp --resource-group $rg --net-framework-version $desiredFramework --min-tls-version $desiredMinTls | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update config on web app '$webApp'." }
    Write-Host "Updated web app '$webApp' configuration."
}
else {
    Write-Host "No drift detected on web app '$webApp'."
}
