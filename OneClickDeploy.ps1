param(
    [switch]$SkipDocker,
    [switch]$SkipBuild,
    [switch]$SkipFrontendBuild,
    [switch]$OpenBrowser = $true,
    [string]$ApiUrl = "http://localhost:5079",
    [int]$FrontendPort = 5173
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

function Stop-StaleHostWindows {
    $hostWindows = Get-CimInstance Win32_Process | Where-Object {
        $_.Name -eq 'powershell.exe' -and (
            $_.CommandLine -like '*CarRental.WebApi*' -or
            $_.CommandLine -like '*npm run dev -- --host localhost --port*'
        )
    }

    foreach ($window in $hostWindows) {
        try {
            Stop-Process -Id $window.ProcessId -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to stop stale host window PID=$($window.ProcessId): $($_.Exception.Message)"
        }
    }
}

function Stop-ProcessesByPort {
    param([Parameter(Mandatory = $true)][int[]]$Ports)

    foreach ($port in $Ports) {
        $connections = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -eq $port }
        if (-not $connections) {
            continue
        }

        $processIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($processId in $processIds) {
            if ($processId -le 0) {
                continue
            }

            try {
                $process = Get-Process -Id $processId -ErrorAction Stop
                if ($process.ProcessName -in @('dotnet', 'node', 'powershell', 'CarRental.WebApi')) {
                    Stop-Process -Id $processId -Force -ErrorAction Stop
                }
            }
            catch {
                Write-Warning "Failed to stop process PID=$processId on port ${port}: $($_.Exception.Message)"
            }
        }
    }
}

function Wait-ContainerHealthy {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [int]$TimeoutSec = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $status = docker inspect $Name --format "{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}" 2>$null
        if ($status -eq 'healthy') {
            return $true
        }

        Start-Sleep -Seconds 2
    }

    return $false
}

$repoRoot = $PSScriptRoot
$solutionPath = Join-Path $repoRoot "CarRentalSystem.sln"
$webAppDir = Join-Path $repoRoot "CarRental.WebApp"
$composePath = Join-Path $repoRoot "deploy\docker-compose.postgres.yml"
$runWebScriptPath = Join-Path $repoRoot "RunWeb.ps1"

Assert-Command -Name "dotnet"
Assert-Command -Name "npm"

Write-Host "[1/5] Stopping previous local hosts..."
Stop-StaleHostWindows
Stop-ProcessesByPort -Ports @(5079, 5173)

Write-Host "[2/5] Ensuring PostgreSQL..."
if (-not $SkipDocker) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker command is not available in PATH."
    }

    docker compose -f $composePath up -d | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Docker compose failed. Start Docker Desktop and retry."
    }

    if (-not (Wait-ContainerHealthy -Name "car-rental-postgres" -TimeoutSec 90)) {
        throw "Container 'car-rental-postgres' did not reach healthy state in time."
    }
}

if (-not (Wait-TcpPortOpen -TargetHost "127.0.0.1" -Port 5432 -TimeoutSec 20)) {
    throw "PostgreSQL is not reachable on 127.0.0.1:5432."
}

Write-Host "[3/5] Building backend..."
if (-not $SkipBuild) {
    dotnet restore $solutionPath | Out-Host
    dotnet build $solutionPath -c Release | Out-Host
}

Write-Host "[4/5] Building frontend..."
if (-not $SkipFrontendBuild) {
    Push-Location $webAppDir
    try {
        if (-not (Test-Path (Join-Path $webAppDir "node_modules"))) {
            npm install | Out-Host
        }

        npm run build | Out-Host
    }
    finally {
        Pop-Location
    }
}

Write-Host "[5/5] Starting API + Web..."
powershell -NoProfile -ExecutionPolicy Bypass -File $runWebScriptPath -SkipDocker -SkipNpmInstall -SkipDotnetRestore -ApiUrl $ApiUrl -FrontendPort $FrontendPort

if ($OpenBrowser) {
    Start-Process "http://localhost:$FrontendPort"
}

Write-Host ""
Write-Host "One-click deploy and restart completed."
Write-Host "API URL: $ApiUrl"
Write-Host "Frontend URL: http://localhost:$FrontendPort"
