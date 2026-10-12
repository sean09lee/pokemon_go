# Creates/updates the SQL server (container) and the SQL database.
# SqlAdminPassword is read from the SQL_ADMIN_PASSWORD environment variable by default
# (mapped from the 'sqlAdminPassword' secret variable in the pipeline).
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'test', 'prod')]
    [string]$Environment,

    [string]$SqlAdminPassword = $env:SQL_ADMIN_PASSWORD
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/_common.ps1"

$names = Get-InfraNames -Environment $Environment
$rg = $names.ResourceGroup
$server = $names.SqlServer
$database = $names.SqlDatabase
$adminUser = $names.SqlAdminUser

# Hardcoded desired state
$desiredMinTls = '1.2'
$desiredEdition = 'Basic'
$desiredCapacity = 5
$desiredMaxSize = '2GB'

# --- SQL server ---
Write-Host "Checking SQL server '$server'..."
$existingServer = Get-AzResourceOrNull "az sql server show --name $server --resource-group $rg"

if ($null -eq $existingServer) {
    if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
        throw "SQL server '$server' does not exist and no admin password was provided. Map the 'sqlAdminPassword' secret variable to the SQL_ADMIN_PASSWORD environment variable."
    }
    Write-Host "SQL server '$server' does not exist. Creating..."
    az sql server create --name $server --resource-group $rg --location $names.Location --admin-user $adminUser --admin-password $SqlAdminPassword --minimal-tls-version $desiredMinTls --enable-public-network true | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create SQL server '$server'." }
    Write-Host "Created SQL server '$server'."
}
else {
    Write-Host "SQL server '$server' exists. Comparing properties..."
    if ($existingServer.minimalTlsVersion -ne $desiredMinTls) {
        Write-Host "Minimum TLS drift detected ('$($existingServer.minimalTlsVersion)' vs '$desiredMinTls'). Updating..."
        az sql server update --name $server --resource-group $rg --minimal-tls-version $desiredMinTls | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to update SQL server '$server'." }
        Write-Host "Updated SQL server '$server'."
    }
    else {
        Write-Host "No drift detected on SQL server '$server'."
    }
}

# --- Firewall rule: allow Azure services (web app) to reach the server ---
Write-Host "Checking firewall rule 'AllowAzureServices'..."
$existingRule = Get-AzResourceOrNull "az sql server firewall-rule show --name AllowAzureServices --server $server --resource-group $rg"
if ($null -eq $existingRule) {
    Write-Host "Firewall rule 'AllowAzureServices' does not exist. Creating..."
    az sql server firewall-rule create --name AllowAzureServices --server $server --resource-group $rg --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create firewall rule on SQL server '$server'." }
    Write-Host "Created firewall rule 'AllowAzureServices'."
}
else {
    Write-Host "Firewall rule 'AllowAzureServices' already exists."
}

# --- SQL database ---
Write-Host "Checking SQL database '$database'..."
$existingDb = Get-AzResourceOrNull "az sql db show --name $database --server $server --resource-group $rg"

if ($null -eq $existingDb) {
    Write-Host "SQL database '$database' does not exist. Creating..."
    az sql db create --name $database --server $server --resource-group $rg --edition $desiredEdition --capacity $desiredCapacity --max-size $desiredMaxSize --tags $script:TagArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create SQL database '$database'." }
    Write-Host "Created SQL database '$database'."
    return
}

Write-Host "SQL database '$database' exists. Comparing properties..."

if ($existingDb.sku.name -ne $desiredEdition) {
    Write-Host "Edition drift detected ('$($existingDb.sku.name)' vs '$desiredEdition'). Updating..."
    az sql db update --name $database --server $server --resource-group $rg --edition $desiredEdition --capacity $desiredCapacity --max-size $desiredMaxSize | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update SQL database '$database'." }
    Write-Host "Updated SQL database '$database'."
}
else {
    Write-Host "No drift detected on SQL database '$database'."
}
