# Creates/updates the Azure AI Foundry workspace (ML Workspace) and project.
#
# The Foundry workspace organizes AI resources and projects. The orchestrator
# is registered as an agent within a project in this workspace.
#
# Orchestrator workspace is always in dev environment (shared resource).
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/_common.ps1"

# Orchestrator workspace always goes to dev (shared resource)
$environment = 'dev'
$names = Get-InfraNames -Environment $environment
$rg = $names.ResourceGroup
$aiFoundryName = $names.AiFoundry
$workspace = $names.AiProject
$location = $names.Location

Write-Host "Azure AI Foundry Workspace Setup"
Write-Host "=================================="
Write-Host "Resource Group: $rg"
Write-Host "AI Foundry Resource: $aiFoundryName"
Write-Host "ML Workspace: $workspace"
Write-Host "Location: $location"
Write-Host ""

# Ensure providers are registered
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

Ensure-ProviderRegistered -Namespace 'Microsoft.MachineLearningServices'

# Get or create the ML Workspace
Write-Host "Checking ML Workspace '$workspace'..."
$existingWorkspace = Get-AzResourceOrNull "az ml workspace show --name $workspace --resource-group $rg"

if ($null -eq $existingWorkspace) {
    Write-Host "ML Workspace '$workspace' does not exist. Creating..."
    
    # Get the AI Foundry resource first (it must exist)
    $aiFoundry = Get-AzResourceOrNull "az cognitiveservices account show --name $aiFoundryName --resource-group $rg"
    if ($null -eq $aiFoundry) {
        throw "Azure AI Foundry resource '$aiFoundryName' not found. Run the AiFoundry.ps1 infrastructure step first."
    }

    # Create the workspace linked to the AI Foundry account
    az ml workspace create `
        --name $workspace `
        --resource-group $rg `
        --location $location `
        --kind 'Default' `
        --tags $script:TagArguments `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create ML Workspace '$workspace'."
    }
    
    Write-Host "Created ML Workspace '$workspace'."
} else {
    Write-Host "ML Workspace '$workspace' already exists."
}

# Get the workspace ID
$workspaceId = az ml workspace show --name $workspace --resource-group $rg --query id -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($workspaceId)) {
    throw "Failed to retrieve ML Workspace '$workspace' ID."
}

Write-Host "ML Workspace ID: $workspaceId"
Write-Host ""


Write-Host "Azure AI Foundry Workspace is ready."
