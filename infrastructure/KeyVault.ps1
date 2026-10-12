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
    return
}

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
