$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

$dockerfile = Get-Content (Join-Path $repoRoot 'Dockerfile') -Raw
$permissionIndex = $dockerfile.IndexOf('chmod a+rx /app/.playwright/node/linux-x64/node')
$appUserIndex = $dockerfile.IndexOf('USER $APP_UID')
$driverCheckIndex = $dockerfile.IndexOf('RUN test -x /app/.playwright/node/linux-x64/node')
if ($dockerfile -notmatch 'chmod -R a\+rX /app/\.playwright' -or
    $permissionIndex -lt 0 -or $appUserIndex -le $permissionIndex -or
    $driverCheckIndex -le $appUserIndex) {
    throw 'The Playwright driver must be accessible and executable, with a check after switching to the non-root app user.'
}

foreach ($path in @('infrastructure/_common.ps1', 'infrastructure/AppService.ps1', 'infrastructure/AiFoundry.ps1', 'infrastructure/SqlDatabase.ps1')) {
    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile((Join-Path $repoRoot $path), [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) { throw ($parseErrors | Out-String) }
}

. "$repoRoot/infrastructure/_common.ps1"
$script:UniqueSuffix = '410c'
foreach ($environment in @('dev', 'test', 'prod')) {
    $names = Get-InfraNames -Environment $environment
    $resources = @{
        ResourceGroup = 'rg'; WebApp = 'web'; AppInsights = 'insights'
        LogAnalytics = 'logs'; AppConfig = 'config'; KeyVault = 'kv'
        SqlServer = 'sql'; SqlDatabase = 'db'; SqlFirewallRule = 'firewall'
        AiFoundry = 'ai'; AiDeployment = 'model'
    }
    foreach ($resource in $resources.Keys) {
        if ($names[$resource] -ne "$environment-$($resources[$resource])-teambuilder410c") {
            throw "Resource naming regression for '$environment/$resource'."
        }
    }
    if ($names.Plan -ne 'shared-plan-teambuilder410c' -or
        $names.PlanResourceGroup -ne 'shared-rg-teambuilder410c' -or
        $names.Registry -ne "${environment}acrteambuilder410c" -or
        $names.Registry -notmatch '^[a-z0-9]{5,50}$' -or
        $names.KeyVault.Length -gt 24) {
        throw "Shared hosting or Azure naming constraint regression for '$environment'."
    }
}

function az {
    if (($args -join ' ') -eq 'account show --query id -o tsv') {
        $global:LASTEXITCODE = 0
        return '00000000-0000-0000-0000-000000000000'
    }
    if (($args -join ' ') -match '^(appservice plan show|webapp show) ') {
        $global:LASTEXITCODE = 0
        return '{"reserved":false,"sku":{"name":"F1"}}'
    }
    throw "Unexpected Azure command: $($args -join ' ')"
}

$blockedWindowsMigration = $false
try {
    & "$repoRoot/infrastructure/AppService.ps1" -Environment dev
}
catch {
    if ($_.Exception.Message -notmatch 'Azure cannot convert these resources to Linux') { throw }
    $blockedWindowsMigration = $true
}
finally {
    Remove-Item Function:az
}
if (-not $blockedWindowsMigration) { throw 'Existing Windows resources were not protected.' }

$global:ContainerTestCommands = [System.Collections.Generic.List[string]]::new()
$global:ContainerTestExisting = $false
$global:ContainerTestAppsExist = $false
function az {
    $command = $args -join ' '
    $global:ContainerTestCommands.Add($command)
    $global:LASTEXITCODE = 0
    switch -Regex ($command) {
        '^account show ' { return '00000000-0000-0000-0000-000000000000' }
        '^provider show --namespace Microsoft.ContainerRegistry ' {
            if ($global:ContainerTestExisting) { return 'Registered' }
            return 'NotRegistered'
        }
        '^provider register --namespace Microsoft.ContainerRegistry --wait$' { return }
        '^group show ' {
            if ($global:ContainerTestExisting) { return '{"name":"shared"}' }
            $global:LASTEXITCODE = 1
            return
        }
        '^appservice plan show .*--query id' { return '/subscriptions/test/resourceGroups/shared-rg-teambuilder24bc/providers/Microsoft.Web/serverfarms/shared-plan-teambuilder24bc' }
        '^appservice plan show ' {
            if ($global:ContainerTestExisting) { return '{"kind":"linux","location":"West US 2","sku":{"name":"B1"}}' }
            $global:LASTEXITCODE = 1
            return
        }
        '^webapp show .*--query id' { return '/subscriptions/test/resourceGroups/test/providers/Microsoft.Web/sites/test' }
        '^webapp show ' {
            if ($global:ContainerTestAppsExist) { return '{"kind":"app,linux,container","httpsOnly":true,"appServicePlanId":"/subscriptions/test/resourceGroups/shared-rg-teambuilder24bc/providers/Microsoft.Web/serverfarms/shared-plan-teambuilder24bc"}' }
            $global:LASTEXITCODE = 1
            return
        }
        '^acr show .*--query id' { return '/subscriptions/test/resourceGroups/test/providers/Microsoft.ContainerRegistry/registries/test' }
        '^acr show ' {
            if ($global:ContainerTestExisting) { return '{"name":"test"}' }
            $global:LASTEXITCODE = 1
            return
        }
        '^webapp config show ' { return '{"linuxFxVersion":"DOCKER|registry/teambuilder:42","minTlsVersion":"1.2"}' }
        '^webapp identity show .*--query principalId' { return '11111111-1111-1111-1111-111111111111' }
        '^webapp identity show ' { return '{"principalId":"11111111-1111-1111-1111-111111111111"}' }
        '^role assignment list ' { return '/existing/assignment' }
        '^(group create|acr create|appservice plan create|webapp create|webapp config set|webapp config appsettings set|webapp log config|resource update) ' { return }
        default { throw "Unexpected Azure command: $command" }
    }
}

try {
    & "$repoRoot/infrastructure/AppService.ps1" -Environment dev
    $registrationIndex = $global:ContainerTestCommands.IndexOf('provider register --namespace Microsoft.ContainerRegistry --wait')
    $registryCreation = $global:ContainerTestCommands | Where-Object { $_ -match '^acr create ' } | Select-Object -First 1
    if ($registrationIndex -lt 0 -or $registrationIndex -ge $global:ContainerTestCommands.IndexOf($registryCreation)) {
        throw 'The registry provider was not registered and awaited before registry creation.'
    }
    if (-not ($global:ContainerTestCommands | Where-Object { $_ -match '^appservice plan create .*--sku B1 --is-linux' }) -or
        -not ($global:ContainerTestCommands | Where-Object { $_ -match '^acr create .*--admin-enabled false' }) -or
        -not ($global:ContainerTestCommands | Where-Object { $_ -match 'properties.acrUseManagedIdentityCreds=true' })) {
        throw 'Linux hosting creation did not configure the plan, registry, or managed-identity image pulls.'
    }
    $devCreation = $global:ContainerTestCommands | Where-Object { $_ -match '^webapp create ' }
    if ($devCreation -notmatch '--plan (?<PlanId>\S+)') { throw 'Dev app did not receive a full shared plan ID.' }
    $sharedPlanId = $Matches.PlanId
    if ($sharedPlanId -notmatch '/resourceGroups/shared-rg-teambuilder24bc/providers/Microsoft.Web/serverfarms/shared-plan-teambuilder24bc$') {
        throw 'Dev app is not using the shared hosting resource group.'
    }
    $global:ContainerTestCommands.Clear()
    $global:ContainerTestExisting = $true
    $global:ContainerTestAppsExist = $true
    & "$repoRoot/infrastructure/AppService.ps1" -Environment dev
    if ($global:ContainerTestCommands | Where-Object { $_ -match '^(provider register|acr create|appservice plan create|webapp create) |--linux-fx-version|--runtime' }) {
        throw 'Infrastructure rerun registered an existing provider, recreated resources, or overwrote the deployed container.'
    }

    $global:ContainerTestCommands.Clear()
    $global:ContainerTestAppsExist = $false
    & "$repoRoot/infrastructure/AppService.ps1" -Environment prod
    $prodCreation = $global:ContainerTestCommands | Where-Object { $_ -match '^webapp create ' }
    if ($prodCreation -notmatch '--plan (?<PlanId>\S+)' -or $Matches.PlanId -ne $sharedPlanId -or
        $prodCreation -notmatch '--resource-group prod-rg-teambuilder24bc ' -or
        ($global:ContainerTestCommands | Where-Object { $_ -match '^appservice plan create ' })) {
        throw 'Production did not reuse the dev shared plan while keeping a separate application resource group.'
    }
}
finally {
    Remove-Item Function:az
    Remove-Variable ContainerTestCommands, ContainerTestExisting, ContainerTestAppsExist -Scope Global
}

foreach ($pipeline in @('.pipelines/teambuilder-build.pipeline.yaml', '.pipelines/teambuilder-release.pipeline.yaml')) {
    $yaml = Get-Content (Join-Path $repoRoot $pipeline) -Raw
    $blocks = [regex]::Matches($yaml, '(?m)^(?<indent> +)(?:script|inlineScript): \|\r?\n(?<body>(?:(?:\k<indent>  .*| *)\r?\n?)+)')
    if ($blocks.Count -eq 0) { throw "No inline scripts found in '$pipeline'." }
    foreach ($block in $blocks) {
        $tokens = $null
        $parseErrors = $null
        [void][System.Management.Automation.Language.Parser]::ParseInput($block.Groups['body'].Value, [ref]$tokens, [ref]$parseErrors)
        if ($parseErrors.Count -gt 0) { throw ($parseErrors | Out-String) }
    }
    if ($yaml -notmatch "vmImage: 'ubuntu-latest'" -or $yaml -notmatch 'image.tar' -or $yaml -match 'zipDeploy|runFromPackage') {
        throw "Pipeline '$pipeline' does not use the Linux container artifact contract."
    }
}

Write-Host 'PowerShell syntax, shared dev/prod B1 plan, separate applications, Windows migration guard, infrastructure rerun, and pipeline contracts passed.'