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
}
else {
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
}

# --- Model deployment ---
# Ministral-3B is the smallest/cheapest chat model available to this subscription
# (verified via 'az cognitiveservices account list-models' - the subscription has
# no OpenAI-format models enabled). GlobalStandard bills per token, so the
# capacity only sets the throughput limit, never the price.
$desiredDeployment = 'Ministral-3B'
$desiredModelName = 'Ministral-3B'
$desiredModelVersion = '1'
$desiredModelFormat = 'Mistral AI'
$desiredDeploymentSku = 'GlobalStandard'
$desiredCapacity = 1

Write-Host "Checking model deployment '$desiredDeployment' on '$aiFoundry'..."
$existingDeployment = Get-AzResourceOrNull "az cognitiveservices account deployment show --name $aiFoundry --resource-group $rg --deployment-name $desiredDeployment"

if ($null -eq $existingDeployment) {
    Write-Host "Model deployment '$desiredDeployment' does not exist. Creating..."
    az cognitiveservices account deployment create --name $aiFoundry --resource-group $rg --deployment-name $desiredDeployment --model-name $desiredModelName --model-version $desiredModelVersion --model-format $desiredModelFormat --sku-name $desiredDeploymentSku --sku-capacity $desiredCapacity | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create model deployment '$desiredDeployment'." }
    Write-Host "Created model deployment '$desiredDeployment'."
}
elseif ($existingDeployment.properties.model.name -ne $desiredModelName -or
        $existingDeployment.properties.model.version -ne $desiredModelVersion -or
        $existingDeployment.sku.name -ne $desiredDeploymentSku) {
    Write-Host "Drift detected on model deployment '$desiredDeployment'. Updating..."
    az cognitiveservices account deployment create --name $aiFoundry --resource-group $rg --deployment-name $desiredDeployment --model-name $desiredModelName --model-version $desiredModelVersion --model-format $desiredModelFormat --sku-name $desiredDeploymentSku --sku-capacity $desiredCapacity | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update model deployment '$desiredDeployment'." }
    Write-Host "Updated model deployment '$desiredDeployment'."
}
else {
    Write-Host "No drift detected on model deployment '$desiredDeployment'."
}

# --- Seed the AIAdvisor settings in App Configuration so the app needs no manual setup ---
# The app reads keys with a null label and with the environment label; these are
# seeded with the environment label. Key auth works because the app itself
# connects via a connection string (local auth stays enabled on the store).
$appConfig = $names.AppConfig
$endpoint = (az cognitiveservices account show --name $aiFoundry --resource-group $rg --query properties.endpoint -o tsv)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($endpoint)) { throw "Failed to resolve the endpoint of Azure AI Foundry resource '$aiFoundry'." }

$aiSettings = @(
    @{ Key = 'AIAdvisor:Endpoint'; Value = $endpoint },
    @{ Key = 'AIAdvisor:DeploymentName'; Value = $desiredDeployment }
)

foreach ($setting in $aiSettings) {
    $current = (az appconfig kv show --name $appConfig --key $setting.Key --label $Environment --auth-mode key --query value -o tsv 2>$null)
    if ($LASTEXITCODE -eq 0 -and $current -eq $setting.Value) {
        Write-Host "App Configuration key '$($setting.Key)' (label '$Environment') is up to date."
        continue
    }

    Write-Host "Setting App Configuration key '$($setting.Key)' (label '$Environment')..."
    az appconfig kv set --name $appConfig --key $setting.Key --value $setting.Value --label $Environment --auth-mode key --yes --output none
    if ($LASTEXITCODE -ne 0) { throw "Failed to set App Configuration key '$($setting.Key)'." }
}
