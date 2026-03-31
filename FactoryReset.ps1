param(
    [switch]$SkipDatabaseReset,
    [switch]$SkipDocker,
    [string]$PostgresConnection = "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres"
)

$ErrorActionPreference = "Stop"

function Assert-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Command '$Name' was not found in PATH."
    }
}

function Test-TcpPortOpen {
    param(
        [Parameter(Mandatory = $true)][string]$TargetHost,
        [Parameter(Mandatory = $true)][int]$Port
    )

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect($TargetHost, $Port, $null, $null)
        $connected = $async.AsyncWaitHandle.WaitOne(1000, $false)
        if (-not $connected) {
            $client.Close()
            return $false
        }

        $client.EndConnect($async) | Out-Null
        $client.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Wait-TcpPortOpen {
    param(
        [Parameter(Mandatory = $true)][string]$TargetHost,
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutSec = 25
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-TcpPortOpen -TargetHost $TargetHost -Port $Port) {
            return $true
        }

        Start-Sleep -Milliseconds 700
    }

    return $false
}

$repoRoot = $PSScriptRoot
$appDataDir = Join-Path $env:LOCALAPPDATA "CarRentalSystem"
$composePath = Join-Path $repoRoot "deploy\docker-compose.postgres.yml"

Write-Host "Factory reset: $appDataDir"

# Desktop кеш і локальні файли можуть тримати старі документи або налаштування,
# тому перед скиданням зупиняємо застосунок і чистимо app data.
$runningProcesses = Get-Process -Name "CarRental.Desktop" -ErrorAction SilentlyContinue
if ($runningProcesses)
{
    Write-Host "Stopping running CarRental.Desktop process..."
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if (Test-Path $appDataDir)
{
    Remove-Item -Path $appDataDir -Recurse -Force
}

New-Item -ItemType Directory -Path $appDataDir -Force | Out-Null

if (-not $SkipDatabaseReset)
{
    # Скидання БД виконується через EF tooling web-проєкту, бо саме він містить актуальну схему міграцій.
    Assert-Command -Name "dotnet"

    if (-not $SkipDocker) {
        Assert-Command -Name "docker"
        docker compose -f $composePath up -d | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Docker compose failed. Start Docker Desktop and retry."
        }
    }

    if (-not (Wait-TcpPortOpen -TargetHost "127.0.0.1" -Port 5432 -TimeoutSec 30)) {
        throw "PostgreSQL is not reachable on 127.0.0.1:5432."
    }

    Push-Location $repoRoot
    try
    {
        $env:CAR_RENTAL_JWT_SIGNING_KEY = "factory-reset-postgres-signing-key-20260313"
        dotnet tool restore
        dotnet tool run dotnet-ef database drop --force `
            --project ".\CarRental.WebApi\CarRental.WebApi.csproj" `
            --startup-project ".\CarRental.WebApi\CarRental.WebApi.csproj" `
            --context RentalDbContext `
            --connection "$PostgresConnection"

        dotnet tool run dotnet-ef database update `
            --project ".\CarRental.WebApi\CarRental.WebApi.csproj" `
            --startup-project ".\CarRental.WebApi\CarRental.WebApi.csproj" `
            --context RentalDbContext `
            --connection "$PostgresConnection"
    }
    finally
    {
        Pop-Location
    }
}

Write-Host ""
Write-Host "Done. Application data and PostgreSQL schema reset to factory state."
Write-Host "On next app or API start, seed accounts will be recreated:"
Write-Host "  admin / admin123"
Write-Host "  manager / manager123"
