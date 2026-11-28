#Requires -Modules Az.Ml

<#
.SYNOPSIS
    Automates creation of Foundry project in Azure AI Foundry.
    
    This script creates a project within an ML Workspace to host orchestrator agents.
    Runs as part of the infrastructure pipeline after AiFoundryWorkspace.ps1.
    
.PARAMETER Environment
    The environment (dev, test, prod) for which to create the project.
#>

param(
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
$location = $names.Location

Write-Host "Creating Foundry Project"
Write-Host "========================"
Write-Host "Environment: $Environment"
Write-Host "Resource Group: $rg"
Write-Host "ML Workspace: $workspace"
Write-Host "Project Name: $projectName"
Write-Host "Location: $location"
Write-Host ""

# Verify ML Workspace exists
Write-Host "Verifying ML Workspace '$workspace' exists..."
$workspace_exists = Get-AzResourceOrNull "az ml workspace show --name $workspace --resource-group $rg"
if ($null -eq $workspace_exists) {
    throw "ML Workspace '$workspace' not found. Run AiFoundryWorkspace.ps1 first."
}

Write-Host "✓ ML Workspace verified: $workspace"
Write-Host ""

# Create the Foundry project using az cli
# Note: This creates a project resource within the workspace
Write-Host "Creating Foundry project: $projectName"

try {
    # Create project using Azure ML SDK
    $project = az ml project create `
        --name $projectName `
        --workspace-name $workspace `
        --resource-group $rg `
        --query '{id: id, name: name}' `
        --output json 2>&1 | ConvertFrom-Json
    
    if ($LASTEXITCODE -eq 0 -and $project -and $project.id) {
        Write-Host "✓ Project created successfully: $projectName"
        Write-Host "  Project ID: $($project.id)"
    } else {
        # Project might already exist, try to get it
        $project = az ml project show `
            --name $projectName `
            --workspace-name $workspace `
            --resource-group $rg `
            --query '{id: id, name: name}' `
            --output json 2>&1 | ConvertFrom-Json
        
        if ($LASTEXITCODE -eq 0 -and $project -and $project.id) {
            Write-Host "✓ Project already exists: $projectName"
            Write-Host "  Project ID: $($project.id)"
        } else {
            throw "Failed to create or verify project: $projectName"
        }
    }
} catch {
    Write-Error "Error creating Foundry project: $_"
    throw
}

# Store project info in App Configuration for orchestrator reference
Write-Host ""
Write-Host "Storing project information in App Configuration..."
$appConfig = $names.AppConfig

$projectSettings = @(
    @{ Key = 'Foundry:ProjectName'; Value = $projectName },
    @{ Key = 'Foundry:ProjectId'; Value = $project.id }
)

foreach ($setting in $projectSettings) {
    Write-Host "  Setting $($setting.Key)..."
    az appconfig kv set --name $appConfig --key $setting.Key --value $setting.Value --auth-mode key --yes --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set App Configuration key '$($setting.Key)'."
    }
}

Write-Host ""
Write-Host "✓ Foundry project created and configured"
Write-Host "  Name: $projectName"
Write-Host "  Workspace: $workspace"
Write-Host "  Environment: $Environment"
Write-Host ""
Write-Host "Project is now ready for orchestrator agents to register."
