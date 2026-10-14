# Creates/updates the Application Insights component.
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
$appInsights = $names.AppInsights
$logAnalytics = $names.LogAnalytics

# Hardcoded desired state
$desiredKind = 'web'
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

Ensure-ProviderRegistered -Namespace 'Microsoft.Insights'
Ensure-ProviderRegistered -Namespace 'Microsoft.OperationalInsights'

# Application Insights is workspace-based. Create the Log Analytics workspace
# explicitly in our resource group first - otherwise Azure auto-provisions a
# "managed" workspace in a separate, auto-named resource group.
Write-Host "Checking Log Analytics workspace '$logAnalytics'..."
$existingWorkspace = Get-AzResourceOrNull "az monitor log-analytics workspace show --workspace-name $logAnalytics --resource-group $rg"

if ($null -eq $existingWorkspace) {
    Write-Host "Log Analytics workspace '$logAnalytics' does not exist. Creating..."
    az monitor log-analytics workspace create --workspace-name $logAnalytics --resource-group $rg --location $names.Location --retention-time $desiredRetentionDays --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create Log Analytics workspace '$logAnalytics'." }
    Write-Host "Created Log Analytics workspace '$logAnalytics'."
}
else {
    Write-Host "Log Analytics workspace '$logAnalytics' exists."
}

$workspaceResourceId = az monitor log-analytics workspace show --workspace-name $logAnalytics --resource-group $rg --query id -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($workspaceResourceId)) {
    throw "Failed to resolve resource id for Log Analytics workspace '$logAnalytics'."
}

Write-Host "Checking Application Insights '$appInsights'..."
$existing = Get-AzResourceOrNull "az monitor app-insights component show --app $appInsights --resource-group $rg"

if ($null -eq $existing) {
    Write-Host "Application Insights '$appInsights' does not exist. Creating..."
    az monitor app-insights component create --app $appInsights --resource-group $rg --location $names.Location --kind $desiredKind --application-type web --workspace $workspaceResourceId --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create Application Insights '$appInsights'." }
    Write-Host "Created Application Insights '$appInsights'."
}
else {
    if ($existing.WorkspaceResourceId -ne $workspaceResourceId) {
        Write-Warning "Application Insights '$appInsights' is linked to a different workspace ('$($existing.WorkspaceResourceId)' vs '$workspaceResourceId'). The workspace link cannot be changed after creation."
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
}

# --- Wire the connection string into the web app so the app actually reports telemetry ---
$webApp = $names.WebApp
$connectionString = az monitor app-insights component show --app $appInsights --resource-group $rg --query connectionString -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($connectionString)) {
    throw "Failed to resolve connection string for Application Insights '$appInsights'."
}

$existingWebApp = Get-AzResourceOrNull "az webapp show --name $webApp --resource-group $rg"
if ($null -eq $existingWebApp) {
    Write-Warning "Web app '$webApp' does not exist yet. Skipping Application Insights wiring; re-run this script after the web app is created."
}
else {
    $currentSetting = az webapp config appsettings list --name $webApp --resource-group $rg --query "[?name=='APPLICATIONINSIGHTS_CONNECTION_STRING'].value" -o tsv
    if ($currentSetting -ne $connectionString) {
        Write-Host "Setting APPLICATIONINSIGHTS_CONNECTION_STRING on web app '$webApp'..."
        az webapp config appsettings set --name $webApp --resource-group $rg --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=$connectionString" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to set APPLICATIONINSIGHTS_CONNECTION_STRING on web app '$webApp'." }
        Write-Host "Configured web app '$webApp' to send telemetry to Application Insights '$appInsights'."
    }
    else {
        Write-Host "Web app '$webApp' is already configured with the current Application Insights connection string."
    }
}
