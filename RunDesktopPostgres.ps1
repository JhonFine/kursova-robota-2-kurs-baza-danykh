param(
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
$desktopProjectPath = Join-Path $repoRoot "CarRental.Desktop\CarRental.Desktop.csproj"
$composePath = Join-Path $repoRoot "deploy\docker-compose.postgres.yml"

Assert-Command -Name "dotnet"

if (-not $SkipDocker) {
    Assert-Command -Name "docker"
    Write-Host "Starting PostgreSQL container..."
    docker compose -f $composePath up -d | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Docker compose failed. Start Docker Desktop and retry."
    }
}

if (-not (Wait-TcpPortOpen -TargetHost "127.0.0.1" -Port 5432 -TimeoutSec 30)) {
    throw "PostgreSQL is not reachable on 127.0.0.1:5432."
}

$env:CAR_RENTAL_POSTGRES_CONNECTION = $PostgresConnection

Write-Host "Starting desktop on PostgreSQL..."
Write-Host "Connection: $PostgresConnection"
dotnet run --project $desktopProjectPath
