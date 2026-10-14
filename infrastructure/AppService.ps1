# Creates/updates the App Service plan and the Blazor web app.
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
$plan = $names.Plan
$webApp = $names.WebApp

# Hardcoded desired state
$desiredPlanSku = 'F1'
$desiredFramework = 'v10.0'
$desiredWindowsRuntimeArg = 'dotnet:10'
$desiredLinuxRuntime = 'DOTNETCORE|10.0'
$desiredLinuxRuntimeArg = '"DOTNETCORE|10.0"'
$desiredMinTls = '1.2'

# --- App Service plan ---
Write-Host "Checking App Service plan '$plan'..."
$existingPlan = Get-AzResourceOrNull "az appservice plan show --name $plan --resource-group $rg"

if ($null -eq $existingPlan) {
    Write-Host "App Service plan '$plan' does not exist. Creating..."
    # Capacity and provisioning errors can be transient; retry with backoff.
    $maxAttempts = 5
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        az appservice plan create --name $plan --resource-group $rg --location $names.Location --sku $desiredPlanSku --is-linux false --tags $script:TagArguments | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        if ($attempt -eq $maxAttempts) { throw "Failed to create App Service plan '$plan' after $maxAttempts attempts." }
        $delay = 30 * $attempt
        Write-Host "Plan creation failed (attempt $attempt/$maxAttempts). Retrying in $delay seconds..."
        Start-Sleep -Seconds $delay
    }
    Write-Host "Created App Service plan '$plan'."

    # Re-read the plan so runtime logic below can rely on plan OS information.
    $existingPlan = Get-AzResourceOrNull "az appservice plan show --name $plan --resource-group $rg"
}
else {
    Write-Host "App Service plan '$plan' exists. Comparing properties..."
    if ($desiredPlanSku -eq 'D1' -and (
        $existingPlan.reserved -eq $true -or
        ($null -ne $existingPlan.kind -and ($existingPlan.kind -as [string]).ToLowerInvariant().Contains('linux'))
    )) {
        throw "The Shared (D1) App Service tier requires a Windows plan. Existing plan '$plan' is Linux and cannot be converted in place."
    }

    if ($existingPlan.sku.name -ne $desiredPlanSku) {
        Write-Host "SKU drift detected ('$($existingPlan.sku.name)' vs '$desiredPlanSku'). Updating..."
        az appservice plan update --name $plan --resource-group $rg --sku $desiredPlanSku | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to update App Service plan '$plan'." }
        Write-Host "Updated App Service plan '$plan' SKU."
    }
    else {
        Write-Host "No drift detected on App Service plan '$plan'."
    }
}

# Detect plan OS from a stable CLI query first, then fall back to object fields.
$reservedValue = (az appservice plan show --name $plan --resource-group $rg --query reserved -o tsv 2>$null)
if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($reservedValue)) {
    $isLinuxPlan = $reservedValue.Trim().ToLowerInvariant() -eq 'true'
}
elseif ($null -ne $existingPlan.reserved) {
    $isLinuxPlan = [bool]$existingPlan.reserved
}
elseif ($null -ne $existingPlan.kind -and ($existingPlan.kind -as [string]).ToLowerInvariant().Contains('linux')) {
    $isLinuxPlan = $true
}
else {
    $isLinuxPlan = $false
}

$planOs = if ($isLinuxPlan) { 'Linux' } else { 'Windows' }
Write-Host "Detected plan OS: $planOs."

# --- Web app ---
Write-Host "Checking web app '$webApp'..."
$existingApp = Get-AzResourceOrNull "az webapp show --name $webApp --resource-group $rg"

if ($null -eq $existingApp) {
    Write-Host "Web app '$webApp' does not exist. Creating..."

    $createFailed = $false

    if ($isLinuxPlan) {
        az webapp create --name $webApp --resource-group $rg --plan $plan --runtime $desiredLinuxRuntimeArg --https-only true --tags $script:TagArguments | Out-Null
    }
    else {
        az webapp create --name $webApp --resource-group $rg --plan $plan --https-only true --tags $script:TagArguments | Out-Null
    }

    if ($LASTEXITCODE -ne 0) {
        $createFailed = $true
    }

    # Some API responses do not expose plan OS consistently; retry with Linux runtime when create fails.
    if ($createFailed -and -not $isLinuxPlan) {
        Write-Host "Initial create failed. Retrying with Linux runtime '$desiredLinuxRuntime'..."
        az webapp create --name $webApp --resource-group $rg --plan $plan --runtime $desiredLinuxRuntimeArg --https-only true --tags $script:TagArguments | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $isLinuxPlan = $true
            $createFailed = $false
        }
    }

    if ($createFailed) { throw "Failed to create web app '$webApp'." }

    if ($isLinuxPlan) {
        az webapp config set --name $webApp --resource-group $rg --linux-fx-version $desiredLinuxRuntimeArg --min-tls-version $desiredMinTls | Out-Null
    }
    else {
        # --runtime (rather than --net-framework-version) also populates the CURRENT_STACK
        # metadata the portal/runtime use to select the stack. Without it the web app's
        # Stack settings show as blank and the app fails to start.
        az webapp config set --name $webApp --resource-group $rg --runtime $desiredWindowsRuntimeArg --min-tls-version $desiredMinTls | Out-Null
    }

    if ($LASTEXITCODE -ne 0) { throw "Failed to configure web app '$webApp'." }
    Write-Host "Created web app '$webApp'."
    return
}

Write-Host "Web app '$webApp' exists. Comparing properties..."

if ($existingApp.httpsOnly -ne $true) {
    Write-Host "HTTPS-only drift detected. Enabling..."
    az webapp update --name $webApp --resource-group $rg --set httpsOnly=true | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to enable HTTPS-only on '$webApp'." }
}

$config = az webapp config show --name $webApp --resource-group $rg | ConvertFrom-Json
$needsConfigUpdate = $false
if ($isLinuxPlan) {
    if ($config.linuxFxVersion -ne $desiredLinuxRuntime) {
        Write-Host "Linux runtime drift detected ('$($config.linuxFxVersion)' vs '$desiredLinuxRuntime')."
        $needsConfigUpdate = $true
    }
}
else {
    if ($config.netFrameworkVersion -ne $desiredFramework) {
        Write-Host ".NET framework drift detected ('$($config.netFrameworkVersion)' vs '$desiredFramework')."
        $needsConfigUpdate = $true
    }
}
if ($config.minTlsVersion -ne $desiredMinTls) {
    Write-Host "Minimum TLS drift detected ('$($config.minTlsVersion)' vs '$desiredMinTls')."
    $needsConfigUpdate = $true
}

if ($needsConfigUpdate) {
    if ($isLinuxPlan) {
        az webapp config set --name $webApp --resource-group $rg --linux-fx-version $desiredLinuxRuntimeArg --min-tls-version $desiredMinTls | Out-Null
    }
    else {
        az webapp config set --name $webApp --resource-group $rg --runtime $desiredWindowsRuntimeArg --min-tls-version $desiredMinTls | Out-Null
    }
    if ($LASTEXITCODE -ne 0) { throw "Failed to update config on web app '$webApp'." }
    Write-Host "Updated web app '$webApp' configuration."
}
else {
    Write-Host "No drift detected on web app '$webApp'."
}
