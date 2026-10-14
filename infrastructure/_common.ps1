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

# The Microsoft Entra group that receives data-plane admin access (add/update/delete)
# to Key Vault secrets and App Configuration key-values in every environment.
# The group's OBJECT ID (GUID) is supplied via the ADMIN_GROUP_OBJECT_ID environment
# variable, mapped from the 'adminGroupObjectId' secret variable on the pipeline so the
# identifier stays out of the repo. The object id is used directly - resolving a
# display name would require Microsoft Graph permissions the service connection lacks.
$script:AdminGroupName = 'teambuilder-admins'  # used only for log messages

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
        LogAnalytics  = "teambuilder-logs$suffix-$unique"
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

# Returns the object id of the admin group, read from the ADMIN_GROUP_OBJECT_ID
# environment variable. Fails with guidance when it is missing or unresolved.
function Get-AdminGroupObjectId {
    $objectId = $env:ADMIN_GROUP_OBJECT_ID
    if ([string]::IsNullOrWhiteSpace($objectId) -or
        $objectId -match '^\$\([^)]+\)$' -or
        $objectId -notmatch '^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$') {
        Write-Warning "ADMIN_GROUP_OBJECT_ID is missing or unresolved ('$objectId')."
        Write-Warning "1. Find the object id of the '$($script:AdminGroupName)' group in the portal"
        Write-Warning "   (Entra ID -> Groups -> $($script:AdminGroupName) -> Overview) or run:"
        Write-Warning "     az ad group show --group $($script:AdminGroupName) --query id -o tsv"
        Write-Warning "2. Define a SECRET pipeline variable named 'adminGroupObjectId' on the"
        Write-Warning "   infrastructure-release pipeline with that GUID as the value. It is mapped"
        Write-Warning "   to ADMIN_GROUP_OBJECT_ID in the Key Vault and App Configuration tasks."
        Write-Warning "   For local runs, set `$env:ADMIN_GROUP_OBJECT_ID before invoking the script."
        throw "Admin group object id is not configured. Define the 'adminGroupObjectId' secret pipeline variable and re-run."
    }

    return $objectId
}

# Grants the admin group the given role at the given resource scope.
# Idempotent: does nothing when the assignment already exists.
function Ensure-RoleAssignment {
    param(
        [Parameter(Mandatory)]
        [string]$Scope,

        [Parameter(Mandatory)]
        [string]$RoleName,

        # Defaults to the admin group for backward compatibility with existing callers.
        [string]$AssigneeObjectId,

        [ValidateSet('Group', 'ServicePrincipal', 'User')]
        [string]$PrincipalType = 'Group',

        # Friendly name used only for log messages.
        [string]$AssigneeDisplayName
    )

    if ([string]::IsNullOrWhiteSpace($AssigneeObjectId)) {
        $AssigneeObjectId = Get-AdminGroupObjectId
        $PrincipalType = 'Group'
        $AssigneeDisplayName = $script:AdminGroupName
    }
    elseif ([string]::IsNullOrWhiteSpace($AssigneeDisplayName)) {
        $AssigneeDisplayName = $AssigneeObjectId
    }

    $existing = az role assignment list --scope $Scope --role $RoleName --query "[?principalId=='$AssigneeObjectId'].id" -o tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($existing | Out-String))) {
        Write-Host "Role '$RoleName' is already assigned to '$AssigneeDisplayName'."
        return
    }

    Write-Host "Granting role '$RoleName' to '$AssigneeDisplayName'..."
    $createOutput = az role assignment create --assignee-object-id $AssigneeObjectId --assignee-principal-type $PrincipalType --role $RoleName --scope $Scope 2>&1
    if ($LASTEXITCODE -ne 0) {
        if (($createOutput | Out-String) -match 'AuthorizationFailed') {
            $subscriptionId = (az account show --query id -o tsv)
            $clientId = (az account show --query user.name -o tsv)
            Write-Host ''
            Write-Host 'The pipeline service principal is not allowed to create role assignments'
            Write-Host '(Microsoft.Authorization/roleAssignments/write). Contributor is not enough.'
            Write-Host "One-time fix - run this locally as yourself (after 'az login'), wait a few"
            Write-Host 'minutes for propagation, then re-run the pipeline:'
            Write-Host "  az role assignment create --assignee $clientId --assignee-principal-type ServicePrincipal --role `"Role Based Access Control Administrator`" --scope /subscriptions/$subscriptionId"
        }

        throw "Failed to create role assignment '$RoleName' for '$AssigneeDisplayName' on scope '$Scope'."
    }

    Write-Host "Granted role '$RoleName' to '$AssigneeDisplayName'."
}

# Ensures the web app has a system-assigned managed identity and returns its principal id.
function Get-WebAppPrincipalId {
    param(
        [Parameter(Mandatory)]
        [string]$WebAppName,

        [Parameter(Mandatory)]
        [string]$ResourceGroup
    )

    $principalId = az webapp identity show --name $WebAppName --resource-group $ResourceGroup --query principalId -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($principalId)) {
        return $null
    }

    return $principalId
}
