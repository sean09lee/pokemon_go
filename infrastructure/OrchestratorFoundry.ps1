#Requires -Modules Az.Ml

<#
.SYNOPSIS
    Registers the TeamBuilder.Orchestrator agents with Azure AI Foundry.
    
    The orchestrator container hosts three specialized agents (PM, Dev, QA) that
    are automatically registered as hosted agents in the Foundry project created
    by FoundryProject.ps1.
    
.PARAMETER ImageUri
    The container image URI (e.g., registry.azurecr.io/orchestrator:v1.0.0)

.PARAMETER ConnectionString
    The App Configuration connection string for environment configuration.

.PARAMETER Label
    The environment label (dev/test/prod) for configuration lookups.

.PARAMETER Environment
    The environment (dev, test, prod) to determine which project to register with.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ImageUri,

    [Parameter(Mandatory=$true)]
    [string]$ConnectionString,

    [Parameter(Mandatory=$true)]
    [string]$Label,

    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'test', 'prod')]
    [string]$Environment
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/_common.ps1"

$names = Get-InfraNames -Environment $Environment
$rg = $names.ResourceGroup
$workspace = $names.AiProject
$projectName = "teambuilder-project-$Environment"

Write-Host "Registering TeamBuilder.Orchestrator Agents"
Write-Host "==========================================="
Write-Host "Environment: $Environment"
Write-Host "Resource Group: $rg"
Write-Host "ML Workspace: $workspace"
Write-Host "Foundry Project: $projectName"
Write-Host "Container Image: $ImageUri"
Write-Host ""

# Verify ML Workspace exists
Write-Host "Verifying ML Workspace '$workspace'..."
$workspace_obj = az ml workspace show `
    --name $workspace `
    --resource-group $rg `
    --query '{id: id, location: location, discoveryUrl: discoveryUrl}' `
    --output json 2>$null | ConvertFrom-Json

if (!$workspace_obj -or !$workspace_obj.id) {
    throw "Failed to retrieve ML Workspace: $workspace. Run infrastructure pipeline first."
}

Write-Host "✓ Workspace verified"
Write-Host "  ID: $($workspace_obj.id)"
Write-Host ""

# Verify Foundry project exists
Write-Host "Verifying Foundry Project '$projectName'..."
$project = az ml project show `
    --name $projectName `
    --workspace-name $workspace `
    --resource-group $rg `
    --query '{id: id, name: name}' `
    --output json 2>$null | ConvertFrom-Json

if (!$project -or !$project.id) {
    throw "Failed to retrieve Foundry Project: $projectName. Run FoundryProject.ps1 first."
}

Write-Host "✓ Project verified"
Write-Host "  ID: $($project.id)"
Write-Host "  Name: $($project.name)"
Write-Host ""

# Configure orchestrator settings in App Configuration
Write-Host "Configuring orchestrator settings in App Configuration..."
$appConfig = $names.AppConfig

$orchestratorSettings = @(
    @{ Key = 'Orchestrator:Enabled'; Value = 'true' },
    @{ Key = 'Orchestrator:Environment'; Value = $Environment },
    @{ Key = 'Orchestrator:ProjectName'; Value = $projectName },
    @{ Key = 'Orchestrator:WorkspaceLocation'; Value = $workspace_obj.location },
    @{ Key = 'Orchestrator:WorkspaceId'; Value = $workspace_obj.id },
    @{ Key = 'Orchestrator:DiscoveryUrl'; Value = $workspace_obj.discoveryUrl }
)

foreach ($setting in $orchestratorSettings) {
    $current = (az appconfig kv show --name $appConfig --key $setting.Key --auth-mode key --query value -o tsv 2>$null)
    if ($LASTEXITCODE -eq 0 -and $current -eq $setting.Value) {
        Write-Host "  ✓ $($setting.Key) is up to date"
        continue
    }

    Write-Host "  Setting $($setting.Key)..."
    az appconfig kv set --name $appConfig --key $setting.Key --value $setting.Value --auth-mode key --yes --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set App Configuration key '$($setting.Key)'."
    }
}

Write-Host ""
Write-Host "✓ Orchestrator Registered Successfully"
Write-Host "======================================="
Write-Host ""
Write-Host "Agents: PM Agent, Dev Agent, QA Agent"
Write-Host "Project: $projectName"
Write-Host "Workspace: $workspace"
Write-Host "Status: Ready for workflows"
Write-Host ""
Write-Host "The container will auto-register agents when it starts."
