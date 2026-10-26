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

foreach ($path in @('infrastructure/_common.ps1', 'infrastructure/AppService.ps1', 'infrastructure/AiFoundry.ps1', 'infrastructure/SqlDatabase.ps1', 'infrastructure/KeyVault.ps1')) {
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

$global:VaultTestCommands = [System.Collections.Generic.List[object]]::new()
$global:VaultTestPrincipal = '22222222-2222-2222-2222-222222222222'
$global:VaultTestAssigned = $false
$originalAdminGroup = $env:ADMIN_GROUP_OBJECT_ID
$env:ADMIN_GROUP_OBJECT_ID = '33333333-3333-3333-3333-333333333333'
function az {
    $command = $args -join ' '
    $global:VaultTestCommands.Add(@($args))
    $global:LASTEXITCODE = 0
    switch -Regex ($command) {
        '^account get-access-token ' {
            $json = @{ oid = $global:VaultTestPrincipal } | ConvertTo-Json -Compress
            $encoded = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($json)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
            return "header.$encoded.signature"
        }
        '^account show --query user.type' { return 'servicePrincipal' }
        '^account show ' { return '00000000-0000-0000-0000-000000000000' }
        '^provider show ' { return 'Registered' }
        '^keyvault show .*--query id' { return '/subscriptions/test/resourceGroups/dev-rg-teambuilder24bc/providers/Microsoft.KeyVault/vaults/dev-kv-teambuilder24bc' }
        '^keyvault show ' { return '{"properties":{"sku":{"name":"standard"},"enableRbacAuthorization":true,"softDeleteRetentionInDays":90}}' }
        '^webapp identity show ' { return '11111111-1111-1111-1111-111111111111' }
        '^role assignment list ' {
            if ($command.Contains($global:VaultTestPrincipal) -and -not $global:VaultTestAssigned) { return }
            return '/existing/assignment'
        }
        '^role assignment create ' { $global:VaultTestAssigned = $true; return }
        default { throw "Unexpected Azure operation: $($args[0..2] -join ' ')" }
    }
}
try {
    & "$repoRoot/infrastructure/KeyVault.ps1" -Environment dev
    $assignment = @($global:VaultTestCommands | Where-Object { ($_[0..2] -join ' ') -eq 'role assignment create' })
    if ($assignment.Count -ne 1 -or
        $assignment[0][[array]::IndexOf($assignment[0], '--assignee-object-id') + 1] -ne $global:VaultTestPrincipal -or
        $assignment[0][[array]::IndexOf($assignment[0], '--role') + 1] -ne 'Key Vault Secrets Officer' -or
        $assignment[0][[array]::IndexOf($assignment[0], '--assignee-principal-type') + 1] -ne 'ServicePrincipal' -or
        $assignment[0][[array]::IndexOf($assignment[0], '--scope') + 1] -notmatch '/vaults/dev-kv-teambuilder24bc$') {
        throw 'Pipeline identity was not granted vault-scoped Key Vault Secrets Officer using its object ID.'
    }
    $global:VaultTestCommands.Clear()
    & "$repoRoot/infrastructure/KeyVault.ps1" -Environment dev
    if ($global:VaultTestCommands | Where-Object { ($_[0..2] -join ' ') -eq 'role assignment create' }) {
        throw 'An existing deployment identity vault role was recreated.'
    }
}
finally {
    Remove-Item Function:az
    Remove-Variable VaultTestCommands, VaultTestPrincipal, VaultTestAssigned -Scope Global
    $env:ADMIN_GROUP_OBJECT_ID = $originalAdminGroup
}

$global:AiTestCommands = [System.Collections.Generic.List[object]]::new()
$global:AiTestSettings = @{}
$global:AiTestSecret = $null
$global:AiTestKey = 'fake-foundry-key-for-tests'
$global:AiTestDenySecretWrite = $false
function az {
    $command = $args -join ' '
    $global:AiTestCommands.Add(@($args))
    $global:LASTEXITCODE = 0
    switch -Regex ($command) {
        '^account show ' { return '00000000-0000-0000-0000-000000000000' }
        '^provider show ' { return 'Registered' }
        '^cognitiveservices account show .*--query properties.endpoint' { return 'https://example.invalid/' }
        '^cognitiveservices account show ' { return '{"kind":"AIServices","sku":{"name":"S0"}}' }
        '^cognitiveservices account deployment show ' { return '{"properties":{"model":{"name":"Ministral-3B","version":"1"}},"sku":{"name":"GlobalStandard"}}' }
        '^cognitiveservices account keys list ' { return $global:AiTestKey }
        '^keyvault show ' { return 'https://test-vault.vault.azure.net/' }
        '^keyvault secret show ' {
            if ($null -eq $global:AiTestSecret) { $global:LASTEXITCODE = 1; return }
            return $global:AiTestSecret
        }
        '^keyvault secret set ' {
            if ($global:AiTestDenySecretWrite) { $global:LASTEXITCODE = 1; return }
            $global:AiTestSecret = $args[[array]::IndexOf($args, '--value') + 1]
            return
        }
        '^appconfig kv (show|set|set-keyvault) ' {
            $key = $args[[array]::IndexOf($args, '--key') + 1]
            $labelIndex = [array]::IndexOf($args, '--label')
            $label = if ($labelIndex -ge 0) { $args[$labelIndex + 1] } else { '' }
            $identity = "$key|$label"
            if ($args[2] -eq 'show') {
                if ($global:AiTestSettings.ContainsKey($identity)) { return $global:AiTestSettings[$identity] }
                $global:LASTEXITCODE = 1
                return
            }
            $valueArgument = if ($args[2] -eq 'set-keyvault') { '--secret-identifier' } else { '--value' }
            $global:AiTestSettings[$identity] = $args[[array]::IndexOf($args, $valueArgument) + 1]
            return
        }
        default { throw "Unexpected Azure operation: $($args[0..2] -join ' ')" }
    }
}

try {
    & "$repoRoot/infrastructure/AiFoundry.ps1" -Environment dev
    $expectedPrompt = "You are an expert Pok$([char]0x00E9)mon team-building advisor named Professor Oak. Help users build balanced, competitive teams by analyzing type coverage, synergy, and common threats. Keep responses concise and actionable."
    if ($global:AiTestSecret -cne $global:AiTestKey -or
        $global:AiTestSettings['AIAdvisor:ApiKey|'] -ne 'https://test-vault.vault.azure.net/secrets/AIAdvisor-ApiKey' -or
        $global:AiTestSettings['AIAdvisor:Endpoint|'] -ne 'https://example.invalid/' -or
        $global:AiTestSettings['AIAdvisor:DeploymentName|'] -ne 'dev-model-teambuilder24bc' -or
        $global:AiTestSettings['AIAdvisor:SystemPrompt|'] -cne $expectedPrompt -or
        $global:AiTestSettings.ContainsKey('AIAdvisor:SystemPrompt|dev')) {
        throw 'AI key vault reference or unlabeled prompt was seeded incorrectly.'
    }
    if ($global:AiTestCommands | Where-Object { $_[0] -eq 'appconfig' -and $_ -contains '--label' }) {
        throw 'Advisor settings must all use the null label.'
    }
    if ($global:AiTestCommands | Where-Object { $_[0] -eq 'appconfig' -and $_ -contains $global:AiTestKey }) {
        throw 'A raw API key was sent to App Configuration.'
    }

    $global:AiTestCommands.Clear()
    & "$repoRoot/infrastructure/AiFoundry.ps1" -Environment dev
    if ($global:AiTestCommands | Where-Object { ($_[0..2] -join ' ') -eq 'keyvault secret set' }) {
        throw 'Unchanged API key created an unnecessary secret version.'
    }

    $global:AiTestKey = 'fake-rotated-foundry-key-for-tests'
    & "$repoRoot/infrastructure/AiFoundry.ps1" -Environment dev
    if ($global:AiTestSecret -cne $global:AiTestKey) { throw 'Rotated API key was not synchronized.' }

    $global:AiTestSecret = $null
    $global:AiTestDenySecretWrite = $true
    $global:AiTestCommands.Clear()
    $permissionFailure = $false
    try { & "$repoRoot/infrastructure/AiFoundry.ps1" -Environment dev }
    catch {
        if ($_.Exception.Message -notmatch 'Key Vault Secrets Officer') { throw }
        $permissionFailure = $true
    }
    if (-not $permissionFailure -or ($global:AiTestCommands | Where-Object { ($_[0..2] -join ' ') -eq 'appconfig kv set-keyvault' })) {
        throw 'Secret write permission failure did not stop reference provisioning.'
    }
}
finally {
    Remove-Item Function:az
    Remove-Variable AiTestCommands, AiTestSettings, AiTestSecret, AiTestKey, AiTestDenySecretWrite -Scope Global
}

Write-Host 'Container deployment, AI prompt labels, Key Vault reference provisioning, API key rotation, and permission failure checks passed.'