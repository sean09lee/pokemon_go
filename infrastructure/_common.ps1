# Shared hardcoded configuration and helpers for the infrastructure deployment scripts.
# Dot-sourced by every Deploy-*.ps1 script.

$script:Location = 'westus2'
$script:DefaultTags = [ordered]@{
    project    = 'team-builder'
    managed_by = 'infrastructure-release.pipeline.yaml'
}
$script:TagArguments = $script:DefaultTags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }

# Hardcoded naming convention. Production keeps the original unsuffixed names.
function Get-InfraNames {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('dev', 'test', 'prod')]
        [string]$Environment
    )

    $suffix = $Environment -eq 'prod' ? '' : "-$Environment"

    [ordered]@{
        Environment   = $Environment
        Location      = $script:Location
        ResourceGroup = "teambuilder-rg$suffix"
        Plan          = "teambuilder-plan$suffix"
        WebApp        = "teambuilder-web$suffix"
        AppInsights   = "teambuilder-insights$suffix"
        AppConfig     = "teambuilder-config$suffix"
        KeyVault      = "teambuilder-kv$suffix"
        SqlServer     = "teambuilder-sql$suffix"
        SqlDatabase   = 'teambuilder'
        SqlAdminUser  = 'teambuilderadmin'
        AiFoundry     = "teambuilder-ai$suffix"
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
