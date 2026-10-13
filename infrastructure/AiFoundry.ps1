# Creates/updates the Azure AI Foundry resource (AIServices multi-service account).
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
$aiFoundry = $names.AiFoundry

# Hardcoded desired state
$desiredKind = 'AIServices'
$desiredSku = 'S0'

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

Ensure-ProviderRegistered -Namespace 'Microsoft.CognitiveServices'

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
