# Creates the resource group. Runs first; every other resource is deployed into it.
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

Write-Host "Checking resource group '$rg'..."
$existing = Get-AzResourceOrNull "az group show --name $rg"

if ($null -eq $existing) {
    Write-Host "Resource group '$rg' does not exist. Creating..."
    az group create --name $rg --location $names.Location --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create resource group '$rg'." }
    Write-Host "Created resource group '$rg'."
    return
}

Write-Host "Resource group '$rg' exists. Comparing properties..."

if ($existing.location -ne $names.Location) {
    Write-Warning "Location drift detected ('$($existing.location)' vs '$($names.Location)'). Location cannot be changed on an existing resource group."
}

$currentTags = @{}
if ($existing.tags) {
    foreach ($p in $existing.tags.PSObject.Properties) { $currentTags[$p.Name] = $p.Value }
}
$tagDrift = $false
foreach ($key in $script:DefaultTags.Keys) {
    if ($currentTags[$key] -ne $script:DefaultTags[$key]) { $tagDrift = $true }
}

if ($tagDrift) {
    Write-Host "Tag drift detected. Updating tags..."
    az group update --name $rg --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update tags on resource group '$rg'." }
    Write-Host "Updated tags on resource group '$rg'."
}
else {
    Write-Host "No drift detected on resource group '$rg'."
}
