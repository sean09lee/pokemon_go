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

# Display name (or object id) of the Microsoft Entra group that receives data-plane
# admin access (add/update/delete) to Key Vault secrets and App Configuration
# key-values in every environment.
$script:AdminGroupName = 'teambuilder-admins'

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

# Resolves (and caches) the object id of the admin group defined above.
function Get-AdminGroupObjectId {
    if (-not $script:AdminGroupObjectId) {
        Write-Host "Resolving admin group '$($script:AdminGroupName)'..."
        $script:AdminGroupObjectId = (az ad group show --group $script:AdminGroupName --query id -o tsv 2>$null)
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($script:AdminGroupObjectId)) {
            $mailNickname = $script:AdminGroupName -replace '[^a-zA-Z0-9]', ''
            Write-Warning "Admin group '$($script:AdminGroupName)' does not exist in Microsoft Entra ID."
            Write-Warning "The pipeline does not create it (that requires Graph permissions the service connection does not have)."
            Write-Warning "Create it once, add yourself, then re-run this pipeline:"
            Write-Warning "  az ad group create --display-name $($script:AdminGroupName) --mail-nickname $mailNickname"
            Write-Warning "  az ad group member add --group $($script:AdminGroupName) --member-id (az ad signed-in-user show --query id -o tsv)"
            throw "Admin group '$($script:AdminGroupName)' was not found. Create it as described above and re-run."
        }

        Write-Host "Resolved admin group '$($script:AdminGroupName)' ($($script:AdminGroupObjectId))."
    }

    return $script:AdminGroupObjectId
}

# Grants the admin group the given role at the given resource scope.
# Idempotent: does nothing when the assignment already exists.
function Ensure-RoleAssignment {
    param(
        [Parameter(Mandatory)]
        [string]$Scope,

        [Parameter(Mandatory)]
        [string]$RoleName
    )

    $assignee = Get-AdminGroupObjectId
    $existing = az role assignment list --scope $Scope --role $RoleName --query "[?principalId=='$assignee'].id" -o tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($existing | Out-String))) {
        Write-Host "Role '$RoleName' is already assigned to group '$($script:AdminGroupName)'."
        return
    }

    Write-Host "Granting role '$RoleName' to group '$($script:AdminGroupName)'..."
    az role assignment create --assignee-object-id $assignee --assignee-principal-type Group --role $RoleName --scope $Scope | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create role assignment '$RoleName' for group '$($script:AdminGroupName)' on scope '$Scope'." }
    Write-Host "Granted role '$RoleName' to group '$($script:AdminGroupName)'."
}
