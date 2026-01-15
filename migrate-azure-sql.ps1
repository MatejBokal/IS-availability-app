#Requires -Version 5.1
# Script to run Entity Framework migrations against Azure SQL Database
# Usage: .\migrate-azure-sql.ps1 -ConnectionString "your-connection-string-here"

param(
    [Parameter(Mandatory=$false)]
    [string]$ConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Azure SQL Migration Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# Find the .csproj file
$projectFile = Get-ChildItem -Path "$scriptDir\AvailabilityCollector" -Filter "*.csproj" | Select-Object -First 1
if ($null -eq $projectFile) {
    Write-Host "ERROR: No .csproj file found" -ForegroundColor Red
    exit 1
}

$projectPath = $projectFile.DirectoryName
$projectFileFullPath = $projectFile.FullName

# If connection string not provided, try to get it from environment variable or prompt
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $envConnectionString = $env:AZURE_SQL_CONNECTION_STRING
    if (-not [string]::IsNullOrWhiteSpace($envConnectionString)) {
        $ConnectionString = $envConnectionString
        Write-Host "Using connection string from AZURE_SQL_CONNECTION_STRING environment variable" -ForegroundColor Gray
    } else {
        Write-Host "Connection string not provided. Please provide it:" -ForegroundColor Yellow
        Write-Host "Example format:" -ForegroundColor Gray
        Write-Host 'Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=yourdatabase;Persist Security Info=False;User ID=youruser;Password=yourpassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;' -ForegroundColor Gray
        Write-Host ""
        $ConnectionString = Read-Host "Enter Azure SQL connection string"
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Host "ERROR: Connection string is required" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Check if EF tools are installed
Write-Host "[1/3] Checking EF Core tools..." -ForegroundColor Magenta
$efCheck = dotnet ef --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Installing EF Core tools..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to install EF Core tools" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "  EF Core tools are installed" -ForegroundColor Green
}

Write-Host ""

# Create temporary appsettings file with Azure connection string
Write-Host "[2/3] Configuring connection string..." -ForegroundColor Magenta
$appsettingsPath = Join-Path $projectPath "appsettings.json"
$appsettingsBackup = Join-Path $projectPath "appsettings.json.backup"

# Backup original appsettings.json
if (Test-Path $appsettingsPath) {
    Copy-Item $appsettingsPath $appsettingsBackup -Force
    Write-Host "  Backed up appsettings.json" -ForegroundColor Gray
}

try {
    # Read and update appsettings.json
    $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    $appsettings.ConnectionStrings.DefaultConnection = $ConnectionString
    
    # Save updated appsettings
    $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
    Write-Host "  Connection string configured" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to update appsettings.json: $($_.Exception.Message)" -ForegroundColor Red
    # Restore backup if it exists
    if (Test-Path $appsettingsBackup) {
        Move-Item $appsettingsBackup $appsettingsPath -Force
    }
    exit 1
}

Write-Host ""

# Run migrations
Write-Host "[3/3] Running Entity Framework migrations..." -ForegroundColor Magenta
Write-Host "  Applying migrations to Azure SQL Database..." -ForegroundColor Yellow

Push-Location $projectPath
try {
    dotnet ef database update --project $projectFileFullPath
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "Migrations applied successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Your database schema has been created/updated in Azure SQL" -ForegroundColor Gray
        Write-Host "2. You can now configure your application to use the Azure SQL connection string" -ForegroundColor Gray
        Write-Host "3. For Azure App Service, set the connection string in Configuration -> Connection strings" -ForegroundColor Gray
    } else {
        Write-Host ""
        Write-Host "ERROR: Migration failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host ""
    Write-Host "ERROR: Migration failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
    
    # Restore original appsettings.json
    if (Test-Path $appsettingsBackup) {
        Write-Host ""
        Write-Host "Restoring original appsettings.json..." -ForegroundColor Gray
        Move-Item $appsettingsBackup $appsettingsPath -Force
    }
}
