#Requires -Version 5.1
# Script to test Azure SQL Database connection
# Usage: .\test-azure-sql.ps1 -ConnectionString "your-connection-string-here"

param(
    [Parameter(Mandatory=$false)]
    [string]$ConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Azure SQL Connection Test" -ForegroundColor Cyan
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
Write-Host "Testing connection..." -ForegroundColor Magenta

# Create a simple test program
$testDir = Join-Path $env:TEMP "azure-sql-test-$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

try {
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
  </ItemGroup>
</Project>
"@
    
    $programContent = @"
using Microsoft.Data.SqlClient;

try {
    var connectionString = "$($ConnectionString.Replace('"', '\"'))";
    using (var connection = new SqlConnection(connectionString)) {
        connection.Open();
        var command = new SqlCommand("SELECT @@VERSION", connection);
        var version = command.ExecuteScalar();
        Console.WriteLine("SUCCESS: Connected to Azure SQL Database!");
        Console.WriteLine($"SQL Server Version: {version}");
        
        command = new SqlCommand("SELECT DB_NAME()", connection);
        var dbName = command.ExecuteScalar();
        Console.WriteLine($"Current Database: {dbName}");
        
        try {
            command = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory'", connection);
            var migrationsTableExists = (int)command.ExecuteScalar() > 0;
            if (migrationsTableExists) {
                command = new SqlCommand("SELECT COUNT(*) FROM __EFMigrationsHistory", connection);
                var migrationCount = command.ExecuteScalar();
                Console.WriteLine($"Migrations Applied: {migrationCount}");
            } else {
                Console.WriteLine("No migrations have been applied yet.");
            }
        } catch (Exception ex) {
            Console.WriteLine($"Note: Could not check migrations table: {ex.Message}");
        }
    }
    return 0;
} catch (Exception ex) {
    Console.WriteLine($"ERROR: {ex.Message}");
    return 1;
}
"@
    
    $csprojFile = Join-Path $testDir "TestConnection.csproj"
    $programFile = Join-Path $testDir "Program.cs"
    
    $csprojContent | Out-File -FilePath $csprojFile -Encoding UTF8
    $programContent | Out-File -FilePath $programFile -Encoding UTF8
    
    Push-Location $testDir
    
    Write-Host "Building test application..." -ForegroundColor Gray
    dotnet build -nologo -v q 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to build test application" -ForegroundColor Red
        Write-Host "Trying to get build errors..." -ForegroundColor Yellow
        dotnet build 2>&1
        $exitCode = 1
    } else {
        Write-Host "Running connection test..." -ForegroundColor Gray
        dotnet run --no-build 2>&1
        $exitCode = $LASTEXITCODE
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
} finally {
    Pop-Location
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""

if ($exitCode -eq 0) {
    Write-Host "Connection test completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Connection test failed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "1. Check your firewall rules - Azure SQL requires your IP to be whitelisted" -ForegroundColor Gray
    Write-Host "2. Verify the connection string format is correct" -ForegroundColor Gray
    Write-Host "3. Ensure the database server name and credentials are correct" -ForegroundColor Gray
    Write-Host "4. Check if 'Allow Azure services and resources to access this server' is enabled" -ForegroundColor Gray
}

exit $exitCode
