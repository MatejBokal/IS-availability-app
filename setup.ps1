#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " AvailabilityCollector Setup Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get the script directory and set it as working directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir
Write-Host "Working directory: $scriptDir" -ForegroundColor Gray

# Find the .csproj file
$csprojFile = Get-ChildItem -Path $scriptDir -Filter "*.csproj" -Recurse | Select-Object -First 1
if ($null -eq $csprojFile) {
    Write-Host "ERROR: No .csproj file found in $scriptDir" -ForegroundColor Red
    exit 1
}
$projectPath = $csprojFile.DirectoryName
$projectFile = $csprojFile.FullName
Write-Host "Project found: $projectFile" -ForegroundColor Gray
Write-Host ""

# Configuration
$containerName = "availability-sqlserver"
$saPassword = "VerySecret!"
$sqlPort = 1433

# Function to check if a command exists
function Test-Command {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Function to wait for SQL Server to be ready
function Wait-ForSqlServer {
    param([int]$MaxAttempts = 30, [int]$DelaySeconds = 2)
    
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $tcp.Connect("localhost", $sqlPort)
            $tcp.Close()
            Write-Host "SQL Server is ready!" -ForegroundColor Green
            return $true
        }
        catch {
            Write-Host "  Attempt $i/$MaxAttempts - SQL Server not ready yet..."
            Start-Sleep -Seconds $DelaySeconds
        }
    }
    return $false
}

# Step 1: Check prerequisites
Write-Host "[1/6] Checking prerequisites..." -ForegroundColor Magenta

if (-not (Test-Command "docker")) {
    Write-Host "ERROR: Docker is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Please install Docker Desktop from https://www.docker.com/products/docker-desktop" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Command "dotnet")) {
    Write-Host "ERROR: .NET SDK is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Please install .NET SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Check if Docker is running
$dockerInfo = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Docker is not running. Please start Docker Desktop." -ForegroundColor Red
    exit 1
}

Write-Host "  Docker: OK" -ForegroundColor Green
Write-Host "  .NET SDK: $(dotnet --version)" -ForegroundColor Green

# Step 2: Start SQL Server container
Write-Host ""
Write-Host "[2/6] Setting up SQL Server container..." -ForegroundColor Magenta

$existingContainer = docker ps -a --filter "name=$containerName" --format "{{.Names}}" 2>&1
$runningContainer = docker ps --filter "name=$containerName" --format "{{.Names}}" 2>&1

if ($runningContainer -eq $containerName) {
    Write-Host "  SQL Server container is already running." -ForegroundColor Green
}
elseif ($existingContainer -eq $containerName) {
    Write-Host "  Starting existing SQL Server container..." -ForegroundColor Yellow
    docker start $containerName | Out-Null
    Write-Host "  Container started." -ForegroundColor Green
}
else {
    Write-Host "  Creating new SQL Server container..." -ForegroundColor Yellow
    
    docker run `
        -e "ACCEPT_EULA=Y" `
        -e "SA_PASSWORD=$saPassword" `
        -p "${sqlPort}:1433" `
        --name $containerName `
        -d mcr.microsoft.com/mssql/server:2022-latest | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create SQL Server container." -ForegroundColor Red
        exit 1
    }
    Write-Host "  Container created." -ForegroundColor Green
}

# Step 3: Wait for SQL Server
Write-Host ""
Write-Host "[3/6] Waiting for SQL Server to initialize..." -ForegroundColor Magenta

Start-Sleep -Seconds 10  # Initial delay for container startup (increased from 5)

if (-not (Wait-ForSqlServer)) {
    Write-Host "WARNING: Could not verify SQL Server is ready. Continuing anyway..." -ForegroundColor Yellow
}

# Additional delay to ensure SQL Server is fully initialized
Start-Sleep -Seconds 5

# Step 4: Restore dependencies
Write-Host ""
Write-Host "[4/6] Restoring NuGet dependencies..." -ForegroundColor Magenta

dotnet restore $projectFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore dependencies." -ForegroundColor Red
    exit 1
}
Write-Host "  Dependencies restored." -ForegroundColor Green

# Step 5: Apply EF migrations
Write-Host ""
Write-Host "[5/6] Applying Entity Framework migrations..." -ForegroundColor Magenta

# Check if EF tools are installed
$efCheck = dotnet ef --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Installing EF Core tools..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

Write-Host "  Applying Identity migrations..." -ForegroundColor Yellow
dotnet ef database update -c AppIdentityContext --project $projectFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to apply Identity migrations." -ForegroundColor Red
    exit 1
}

Write-Host "  Applying Domain migrations..." -ForegroundColor Yellow
dotnet ef database update -c AppContextDb --project $projectFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to apply Domain migrations." -ForegroundColor Red
    exit 1
}

Write-Host "  Migrations applied." -ForegroundColor Green

# Step 6: Complete
Write-Host ""
Write-Host "[6/6] Setup complete!" -ForegroundColor Magenta
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Setup completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run the application:" -ForegroundColor Yellow
Write-Host "  dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Admin login credentials:" -ForegroundColor Yellow
Write-Host "  Email:    example@example.si" -ForegroundColor White
Write-Host "  Password: Example123." -ForegroundColor White
Write-Host ""

# Ask if user wants to run the app
$runApp = Read-Host "Do you want to run the application now? (y/N)"
if ($runApp -eq "y" -or $runApp -eq "Y") {
    Write-Host ""
    Write-Host "Starting application..." -ForegroundColor Cyan
    Set-Location $projectPath
    dotnet run --project $projectFile
}
