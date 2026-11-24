#Requires -Modules Az.Ml

<#
.SYNOPSIS
    Deploys the TeamBuilder.Orchestrator as a shared hosted agent in Azure AI Foundry.
    
    The orchestrator is a shared resource across all environments (dev, test, prod)
    and is deployed to the dev resource group/workspace.
    
.PARAMETER ImageUri
    The container image URI (e.g., registry.azurecr.io/orchestrator:v1.0.0)
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ImageUri
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/_common.ps1"

# Orchestrator is always deployed to dev (shared resource)
$environment = 'dev'
$names = Get-InfraNames -Environment $environment

Write-Host "Deploying TeamBuilder.Orchestrator to Azure AI Foundry (Shared Resource)"
Write-Host "Resource Group: $($names.ResourceGroup)"
Write-Host "AI Project: $($names.AiProject)"
Write-Host "Container Image: $ImageUri"

# Get the AI project resource
$project = az ml workspace show `
    --name $names.AiProject `
    --resource-group $names.ResourceGroup `
    --query '{id: id, location: location}' `
    --output json | ConvertFrom-Json

if (!$project -or !$project.id) {
    throw "Failed to retrieve AI Foundry project: $($names.AiProject)"
}

$projectId = $project.id
$projectLocation = $project.location

Write-Host "AI Project ID: $projectId"
Write-Host "Project Location: $projectLocation"

# Create or update the hosted agent deployment
$deploymentName = "teambuilder-orchestrator"
$agentName = "TeamBuilderOrchestrator"

Write-Host "Creating/updating hosted agent deployment: $deploymentName"

try {
    $deployment = az ml deployment create `
        --name $deploymentName `
        --workspace-name $names.AiProject `
        --resource-group $names.ResourceGroup `
        --image-uri $ImageUri `
        --output json `
        2>&1 | ConvertFrom-Json

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully created deployment: $deploymentName"
        Write-Host "Deployment ID: $($deployment.id)"
    } else {
        Write-Host "Deployment creation output: $deployment"
        # If deployment already exists, attempt update
        $deployment = az ml deployment update `
            --name $deploymentName `
            --workspace-name $names.AiProject `
            --resource-group $names.ResourceGroup `
            --image-uri $ImageUri `
            --output json `
            2>&1 | ConvertFrom-Json

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully updated deployment: $deploymentName"
            Write-Host "Deployment ID: $($deployment.id)"
        } else {
            throw "Failed to create or update deployment: $deployment"
        }
    }
} catch {
    Write-Error "Error deploying hosted agent: $_"
    throw
}

Write-Host "TeamBuilder.Orchestrator deployed successfully to Azure AI Foundry"
Write-Host "Agent: $agentName"
Write-Host "Resource Group: $($names.ResourceGroup)"
Write-Host "Workspace: $($names.AiProject)"
