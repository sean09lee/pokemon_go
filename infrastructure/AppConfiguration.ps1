# Creates/updates the App Configuration store.
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
$appConfig = $names.AppConfig

# Hardcoded desired state
$desiredSku = 'Standard'

function Ensure-ProviderRegistered {
    param(
        [Parameter(Mandatory)]
        [string]$Namespace
    )

    $state = (az provider show --namespace $Namespace --query registrationState -o tsv 2>$null)
    if ($LASTEXITCODE -eq 0 -and $state -eq 'Registered') {
        Write-Host "Resource provider '$Namespace' is already registered."
        return
    }

    Write-Host "Registering resource provider '$Namespace'..."
    az provider register --namespace $Namespace --wait | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to register resource provider '$Namespace'."
    }

    Write-Host "Resource provider '$Namespace' is registered."
}

Ensure-ProviderRegistered -Namespace 'Microsoft.AppConfiguration'

Write-Host "Checking App Configuration '$appConfig'..."
$existing = Get-AzResourceOrNull "az appconfig show --name $appConfig --resource-group $rg"

if ($null -eq $existing) {
    Write-Host "App Configuration '$appConfig' does not exist. Creating..."
    az appconfig create --name $appConfig --resource-group $rg --location $names.Location --sku $desiredSku --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create App Configuration '$appConfig'." }
    Write-Host "Created App Configuration '$appConfig'."
}
else {
    Write-Host "App Configuration '$appConfig' exists. Comparing properties..."

    if ($existing.sku.name -ne $desiredSku) {
        Write-Host "SKU drift detected ('$($existing.sku.name)' vs '$desiredSku'). Updating..."
        az appconfig update --name $appConfig --resource-group $rg --sku $desiredSku | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to update App Configuration '$appConfig'." }
        Write-Host "Updated App Configuration '$appConfig' SKU."
    }
    else {
        Write-Host "No drift detected on App Configuration '$appConfig'."
    }
}

# --- RBAC: grant the admin group add/update/delete access to key-values ---
$appConfigScope = (az appconfig show --name $appConfig --resource-group $rg --query id -o tsv)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($appConfigScope)) { throw "Failed to resolve the resource id for App Configuration '$appConfig'." }

Ensure-RoleAssignment -Scope $appConfigScope -RoleName 'App Configuration Data Owner'

# --- RBAC: grant the web app's managed identity read access to key-values ---
$webApp = $names.WebApp
$webAppPrincipalId = Get-WebAppPrincipalId -WebAppName $webApp -ResourceGroup $rg
if ($null -eq $webAppPrincipalId) {
    Write-Warning "Web app '$webApp' does not have a managed identity yet. Skipping App Configuration access grant; re-run this script after the web app's identity is assigned."
}
else {
    Ensure-RoleAssignment -Scope $appConfigScope -RoleName 'App Configuration Data Reader' -AssigneeObjectId $webAppPrincipalId -PrincipalType 'ServicePrincipal' -AssigneeDisplayName "web app '$webApp'"
}
