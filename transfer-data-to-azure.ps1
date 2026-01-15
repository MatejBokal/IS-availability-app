#Requires -Version 5.1
# Script to transfer data from local Docker SQL Server to Azure SQL Database
# Usage: .\transfer-data-to-azure.ps1 -LocalConnectionString "local-connection-string" -AzureConnectionString "azure-connection-string"

param(
    [Parameter(Mandatory=$false)]
    [string]$LocalConnectionString,
    
    [Parameter(Mandatory=$false)]
    [string]$AzureConnectionString,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Data Transfer to Azure SQL" -ForegroundColor Cyan
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

# Get connection strings
if ([string]::IsNullOrWhiteSpace($LocalConnectionString)) {
    $LocalConnectionString = "Server=localhost;Database=AvailabilityCollectorDB;User Id=sa;Password=VerySecret!;TrustServerCertificate=true"
    Write-Host "Using default local connection string" -ForegroundColor Gray
}

if ([string]::IsNullOrWhiteSpace($AzureConnectionString)) {
    $envConnectionString = $env:AZURE_SQL_CONNECTION_STRING
    if (-not [string]::IsNullOrWhiteSpace($envConnectionString)) {
        $AzureConnectionString = $envConnectionString
        Write-Host "Using Azure connection string from AZURE_SQL_CONNECTION_STRING environment variable" -ForegroundColor Gray
    } else {
        Write-Host "Azure connection string not provided. Please provide it:" -ForegroundColor Yellow
        $AzureConnectionString = Read-Host "Enter Azure SQL connection string"
    }
}

if ([string]::IsNullOrWhiteSpace($AzureConnectionString)) {
    Write-Host "ERROR: Azure connection string is required" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "This script will transfer data from your local database to Azure SQL." -ForegroundColor Yellow
Write-Host "WARNING: This will append data to Azure SQL. Existing data may cause conflicts." -ForegroundColor Yellow
Write-Host ""

if (-not $Force) {
    $confirm = Read-Host "Continue? (y/N)"
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Host "Cancelled." -ForegroundColor Gray
        exit 0
    }
}

Write-Host ""

# Create a console app to transfer data
$transferDir = Join-Path $env:TEMP "azure-data-transfer-$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $transferDir -Force | Out-Null

try {
    # Create .csproj file
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
    
    $csprojFile = Join-Path $transferDir "DataTransfer.csproj"
    $csprojContent | Out-File -FilePath $csprojFile -Encoding UTF8
    
    # Create Program.cs
    $programContent = @"
using Microsoft.Data.SqlClient;
using System.Data;

var localConnStr = @"$($LocalConnectionString.Replace('"', '""'))";
var azureConnStr = @"$($AzureConnectionString.Replace('"', '""'))";

Console.WriteLine("Starting data transfer...");
Console.WriteLine("");

// List of tables in dependency order (Identity tables first, then domain tables)
var tables = new[]
{
    // Identity tables (must be first)
    "AspNetRoles",
    "AspNetUsers",
    "AspNetRoleClaims",
    "AspNetUserClaims",
    "AspNetUserLogins",
    "AspNetUserRoles",
    "AspNetUserTokens",
    
    // Domain tables (in dependency order)
    "AppSettings",
    "Positions",
    "AvailabilityMonths",
    "Holidays",
    "ShiftMatrices",
    "PositionShifts",
    "ShiftEntries",
    "AvailabilitySubmissions",
    "AvailabilityEntries",
    "Notifications"
};

using (var localConn = new SqlConnection(localConnStr))
using (var azureConn = new SqlConnection(azureConnStr))
{
    localConn.Open();
    Console.WriteLine("Connected to local database");
    
    azureConn.Open();
    Console.WriteLine("Connected to Azure database");
    Console.WriteLine("");
    
    foreach (var tableName in tables)
    {
        try
        {
            Console.Write($"Transferring {tableName}... ");
            
            // Check if table exists in local database
            var checkTableSql = $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
            using (var cmd = new SqlCommand(checkTableSql, localConn))
            {
                var exists = (int)cmd.ExecuteScalar() > 0;
                if (!exists)
                {
                    Console.WriteLine("SKIP (table doesn't exist locally)");
                    continue;
                }
            }
            
            // Get data from local database
            var selectSql = $@"SELECT * FROM [{tableName}]";
            using (var adapter = new SqlDataAdapter(selectSql, localConn))
            {
                var dataTable = new DataTable();
                adapter.Fill(dataTable);
                
                if (dataTable.Rows.Count == 0)
                {
                    Console.WriteLine("SKIP (no data)");
                    continue;
                }
                
                // Insert into Azure database
                using (var bulkCopy = new SqlBulkCopy(azureConn, SqlBulkCopyOptions.KeepIdentity, null))
                {
                    bulkCopy.DestinationTableName = $"[{tableName}]";
                    bulkCopy.BulkCopyTimeout = 300; // 5 minutes
                    bulkCopy.WriteToServer(dataTable);
                }
                
                Console.WriteLine($"OK ({dataTable.Rows.Count} rows)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            // Continue with next table
        }
    }
}

Console.WriteLine("");
Console.WriteLine("Data transfer completed!");
"@
    
    $programFile = Join-Path $transferDir "Program.cs"
    $programContent | Out-File -FilePath $programFile -Encoding UTF8
    
    Push-Location $transferDir
    
    Write-Host "[1/2] Building data transfer tool..." -ForegroundColor Magenta
    dotnet build -nologo -v q 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to build data transfer tool" -ForegroundColor Red
        dotnet build 2>&1
        exit 1
    }
    
    Write-Host "[2/2] Transferring data..." -ForegroundColor Magenta
    Write-Host ""
    dotnet run --no-build 2>&1
    
    $exitCode = $LASTEXITCODE
    
} catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
} finally {
    Pop-Location
    Remove-Item -Path $transferDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""

if ($exitCode -eq 0) {
    Write-Host "Data transfer completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: Identity data (users, roles) has been transferred." -ForegroundColor Cyan
    Write-Host "You may need to reset passwords or use the seed data functionality." -ForegroundColor Cyan
} else {
    Write-Host "Data transfer completed with errors." -ForegroundColor Yellow
    Write-Host "Some tables may not have been transferred. Check the output above." -ForegroundColor Yellow
}

exit $exitCode
