# Creates/updates the Azure AI Foundry resource (AIServices multi-service account).
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
$aiFoundry = $names.AiFoundry

# Hardcoded desired state
$desiredKind = 'AIServices'
$desiredSku = 'S0'

Write-Host "Checking Azure AI Foundry resource '$aiFoundry'..."
$existing = Get-AzResourceOrNull "az cognitiveservices account show --name $aiFoundry --resource-group $rg"

if ($null -eq $existing) {
    Write-Host "Azure AI Foundry resource '$aiFoundry' does not exist. Creating..."
    az cognitiveservices account create --name $aiFoundry --resource-group $rg --location $names.Location --kind $desiredKind --sku $desiredSku --custom-domain $aiFoundry --yes --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create Azure AI Foundry resource '$aiFoundry'." }
    Write-Host "Created Azure AI Foundry resource '$aiFoundry'."
    return
}

Write-Host "Azure AI Foundry resource '$aiFoundry' exists. Comparing properties..."

if ($existing.kind -ne $desiredKind) {
    Write-Warning "Kind drift detected ('$($existing.kind)' vs '$desiredKind'). Kind cannot be changed on an existing account."
}

if ($existing.sku.name -ne $desiredSku) {
    Write-Host "SKU drift detected ('$($existing.sku.name)' vs '$desiredSku'). Updating..."
    az cognitiveservices account update --name $aiFoundry --resource-group $rg --sku $desiredSku | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update Azure AI Foundry resource '$aiFoundry'." }
    Write-Host "Updated Azure AI Foundry resource '$aiFoundry' SKU."
}
else {
    Write-Host "No drift detected on Azure AI Foundry resource '$aiFoundry'."
}
