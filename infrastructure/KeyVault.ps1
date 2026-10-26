# Creates/updates the Key Vault.
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
$keyVault = $names.KeyVault

# Hardcoded desired state
$desiredSku = 'standard'
$desiredRbacAuthorization = $true
$desiredRetentionDays = 90

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

Ensure-ProviderRegistered -Namespace 'Microsoft.KeyVault'

Write-Host "Checking Key Vault '$keyVault'..."
$existing = Get-AzResourceOrNull "az keyvault show --name $keyVault --resource-group $rg"

if ($null -eq $existing) {
    Write-Host "Key Vault '$keyVault' does not exist. Creating..."
    az keyvault create --name $keyVault --resource-group $rg --location $names.Location --sku $desiredSku --enable-rbac-authorization $desiredRbacAuthorization --retention-days $desiredRetentionDays --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create Key Vault '$keyVault'." }
    Write-Host "Created Key Vault '$keyVault'."
}
else {
    Write-Host "Key Vault '$keyVault' exists. Comparing properties..."

    if ($existing.properties.sku.name -ne $desiredSku) {
        Write-Warning "SKU drift detected ('$($existing.properties.sku.name)' vs '$desiredSku'). SKU cannot be changed via az keyvault update; change it manually if required."
    }

    $needsUpdate = $false
    if ($existing.properties.enableRbacAuthorization -ne $desiredRbacAuthorization) {
        Write-Host "RBAC authorization drift detected ('$($existing.properties.enableRbacAuthorization)' vs '$desiredRbacAuthorization')."
        $needsUpdate = $true
    }
    if ($existing.properties.softDeleteRetentionInDays -ne $desiredRetentionDays) {
        Write-Host "Soft-delete retention drift detected ('$($existing.properties.softDeleteRetentionInDays)' vs '$desiredRetentionDays')."
        $needsUpdate = $true
    }

    if ($needsUpdate) {
        az keyvault update --name $keyVault --resource-group $rg --enable-rbac-authorization $desiredRbacAuthorization --retention-days $desiredRetentionDays | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to update Key Vault '$keyVault'." }
        Write-Host "Updated Key Vault '$keyVault'."
    }
    else {
        Write-Host "No drift detected on Key Vault '$keyVault'."
    }
}

# --- RBAC: grant the admin group add/update/delete access to secrets ---
$keyVaultScope = (az keyvault show --name $keyVault --resource-group $rg --query id -o tsv)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($keyVaultScope)) { throw "Failed to resolve the resource id for Key Vault '$keyVault'." }

Ensure-RoleAssignment -Scope $keyVaultScope -RoleName 'Key Vault Secrets Officer'

$accessToken = az account get-access-token --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accessToken)) {
    throw 'Failed to resolve the signed-in identity for Key Vault secret provisioning.'
}
try {
    $payload = $accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/')
    $payload = $payload.PadRight($payload.Length + ((4 - $payload.Length % 4) % 4), '=')
    $claims = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
    $pipelinePrincipalId = $claims.oid
    if ($pipelinePrincipalId -notmatch '^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$') {
        throw 'The signed-in Azure token does not contain a valid principal object ID.'
    }
}
finally {
    $accessToken = $null
    $payload = $null
    $claims = $null
}
$accountType = az account show --query user.type -o tsv
if ($LASTEXITCODE -ne 0 -or $accountType -notin @('user', 'servicePrincipal')) {
    throw 'Failed to determine the signed-in Azure principal type.'
}
$principalType = if ($accountType -eq 'servicePrincipal') { 'ServicePrincipal' } else { 'User' }
Ensure-RoleAssignment -Scope $keyVaultScope -RoleName 'Key Vault Secrets Officer' -AssigneeObjectId $pipelinePrincipalId -PrincipalType $principalType -AssigneeDisplayName 'infrastructure deployment identity'

# --- RBAC: grant the web app's managed identity read access to secrets ---
$webApp = $names.WebApp
$webAppPrincipalId = Get-WebAppPrincipalId -WebAppName $webApp -ResourceGroup $rg
if ($null -eq $webAppPrincipalId) {
    Write-Warning "Web app '$webApp' does not have a managed identity yet. Skipping Key Vault access grant; re-run this script after the web app's identity is assigned."
}
else {
    Ensure-RoleAssignment -Scope $keyVaultScope -RoleName 'Key Vault Secrets User' -AssigneeObjectId $webAppPrincipalId -PrincipalType 'ServicePrincipal' -AssigneeDisplayName "web app '$webApp'"
}
