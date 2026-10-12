# Creates/updates the Application Insights component.
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
$appInsights = $names.AppInsights

# Hardcoded desired state
$desiredKind = 'web'
$desiredRetentionDays = 90

Write-Host "Checking Application Insights '$appInsights'..."
$existing = Get-AzResourceOrNull "az monitor app-insights component show --app $appInsights --resource-group $rg"

if ($null -eq $existing) {
    Write-Host "Application Insights '$appInsights' does not exist. Creating..."
    az monitor app-insights component create --app $appInsights --resource-group $rg --location $names.Location --kind $desiredKind --application-type web --retention-time $desiredRetentionDays --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create Application Insights '$appInsights'." }
    Write-Host "Created Application Insights '$appInsights'."
    return
}

Write-Host "Application Insights '$appInsights' exists. Comparing properties..."

if ($existing.kind -ne $desiredKind) {
    Write-Warning "Kind drift detected ('$($existing.kind)' vs '$desiredKind'). Kind cannot be changed on an existing component."
}

if ($existing.RetentionInDays -ne $desiredRetentionDays) {
    Write-Host "Retention drift detected ('$($existing.RetentionInDays)' vs '$desiredRetentionDays'). Updating..."
    az monitor app-insights component update --app $appInsights --resource-group $rg --retention-time $desiredRetentionDays | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update Application Insights '$appInsights'." }
    Write-Host "Updated Application Insights '$appInsights' retention."
}
else {
    Write-Host "No drift detected on Application Insights '$appInsights'."
}
