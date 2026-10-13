# Shared hardcoded configuration and helpers for the infrastructure deployment scripts.
# Dot-sourced by every Deploy-*.ps1 script.

$script:Location = 'westus2'
$script:DefaultTags = [ordered]@{
    project    = 'team-builder'
    managed_by = 'infrastructure-release.pipeline.yaml'
}
$script:TagArguments = $script:DefaultTags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }

# Deterministic 4-character suffix appended to every resource name. DNS-based names
# (SQL server, Key Vault, App Configuration, web apps) must be unique across ALL of
# Azure, so hashing the subscription id keeps collisions extremely unlikely while
# staying stable between pipeline runs (the scripts remain idempotent). 4 hex chars
# is the most that fits Key Vault's 24-character limit ('teambuilder-kv-test-xxxx').
# If a collision ever occurs, bump $script:UniqueSalt to re-roll every suffix.
$script:UniqueSalt = 'v1'

function Get-UniqueSuffix {
    if (-not $script:UniqueSuffix) {
        $subscriptionId = (az account show --query id -o tsv)
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($subscriptionId)) {
            throw "Failed to resolve the current subscription id via 'az account show'."
        }

        $hash = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes("$($subscriptionId.Trim())$($script:UniqueSalt)"))
        $script:UniqueSuffix = ([System.BitConverter]::ToString($hash) -replace '-', '').ToLower().Substring(0, 4)
    }

    return $script:UniqueSuffix
}

# Hardcoded naming convention. Every name gets the unique suffix; production
# additionally omits the environment segment.
function Get-InfraNames {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('dev', 'test', 'prod')]
        [string]$Environment
    )

    $suffix = $Environment -eq 'prod' ? '' : "-$Environment"
    $unique = Get-UniqueSuffix

    [ordered]@{
        Environment   = $Environment
        Location      = $script:Location
        ResourceGroup = "teambuilder-rg$suffix-$unique"
        Plan          = "teambuilder-plan$suffix-$unique"
        WebApp        = "teambuilder-web$suffix-$unique"
        AppInsights   = "teambuilder-insights$suffix-$unique"
        AppConfig     = "teambuilder-config$suffix-$unique"
        KeyVault      = "teambuilder-kv$suffix-$unique"
        SqlServer     = "teambuilder-sql$suffix-$unique"
        SqlDatabase   = 'teambuilder'
        SqlAdminUser  = 'teambuilderadmin'
        AiFoundry     = "teambuilder-ai$suffix-$unique"
    }
}

# Runs an az show-style command passed as a string, returns $null when the resource does not exist.
function Get-AzResourceOrNull {
    param(
        [Parameter(Mandatory)]
        [string]$ShowCommand
    )

    $output = Invoke-Expression "$ShowCommand 2>`$null"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($output | Out-String))) {
        return $null
    }

    return ($output | ConvertFrom-Json)
}
