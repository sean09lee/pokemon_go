# Creates/updates the App Configuration store.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/_common.ps1"

$names = Get-InfraNames -Environment $Environment
$rg = $names.ResourceGroup
$appConfig = $names.AppConfig

# Hardcoded desired state
$desiredSku = 'Standard'

Write-Host "Checking App Configuration '$appConfig'..."
$existing = Get-AzResourceOrNull "az appconfig show --name $appConfig --resource-group $rg"

if ($null -eq $existing) {
    Write-Host "App Configuration '$appConfig' does not exist. Creating..."
    az appconfig create --name $appConfig --resource-group $rg --location $names.Location --sku $desiredSku --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create App Configuration '$appConfig'." }
    Write-Host "Created App Configuration '$appConfig'."
    return
}

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
