param(
    [switch]$SkipDocker,
    [switch]$SkipNpmInstall,
    [switch]$SkipDotnetRestore,
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
                if ($process.ProcessName -in @('dotnet', 'node', 'powershell', 'cmd')) {
                    Stop-Process -Id $processId -Force -ErrorAction Stop
                }
            }
            catch {
                Write-Warning "Failed to stop process PID=$processId on port ${port}: $($_.Exception.Message)"
            }
        }
    }
}

$repoRoot = $PSScriptRoot
$solutionPath = Join-Path $repoRoot "CarRentalSystem.sln"
$apiProjectPath = Join-Path $repoRoot "CarRental.WebApi\\CarRental.WebApi.csproj"
$webAppDir = Join-Path $repoRoot "CarRental.WebApp"
$envLocalPath = Join-Path $webAppDir ".env.local"
$composePath = Join-Path $repoRoot "deploy\\docker-compose.postgres.yml"
$apiUri = [Uri]$ApiUrl
$apiHost = $apiUri.Host
$apiPort = if ($apiUri.IsDefaultPort) { if ($apiUri.Scheme -eq 'https') { 443 } else { 80 } } else { $apiUri.Port }

Assert-Command -Name "dotnet"
Assert-Command -Name "npm"

Write-Host "Stopping previous local hosts on API/frontend ports..."
Stop-StaleHostWindows
Stop-ProcessesByPort -Ports @($apiPort, $FrontendPort)

if (-not $SkipDocker) {
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        Write-Host "Starting PostgreSQL via docker compose..."
        docker compose -f $composePath up -d | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Docker compose failed. Start Docker Desktop and retry."
        }
    }
    else {
        Write-Warning "Docker command is not available. Skipping PostgreSQL start."
    }
}

if (-not (Wait-TcpPortOpen -TargetHost "127.0.0.1" -Port 5432 -TimeoutSec 30)) {
    throw "PostgreSQL is not reachable on 127.0.0.1:5432. Start postgres (docker/service) and rerun RunWeb.ps1."
}

if (-not $SkipDotnetRestore) {
    Write-Host "Restoring .NET dependencies..."
    dotnet restore $solutionPath | Out-Host
}

if (-not $SkipNpmInstall -and -not (Test-Path (Join-Path $webAppDir "node_modules"))) {
    Write-Host "Installing npm dependencies..."
    Push-Location $webAppDir
    try {
        npm install | Out-Host
    }
    finally {
        Pop-Location
    }
}

"VITE_API_BASE_URL=$ApiUrl" | Set-Content $envLocalPath -Encoding UTF8

$jwtSigningKey = [Environment]::GetEnvironmentVariable("CAR_RENTAL_JWT_SIGNING_KEY")
if ([string]::IsNullOrWhiteSpace($jwtSigningKey)) {
    $jwtSigningKey = ("{0}{1}" -f ([guid]::NewGuid().ToString("N")), ([guid]::NewGuid().ToString("N")))
}
$jwtSigningKeyEscaped = $jwtSigningKey.Replace("'", "''")

$apiCommand = "Set-Location '$repoRoot'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:ASPNETCORE_URLS='$ApiUrl'; `$env:CAR_RENTAL_JWT_SIGNING_KEY='$jwtSigningKeyEscaped'; dotnet run --project '.\\CarRental.WebApi\\CarRental.WebApi.csproj'"
$webCommand = "Set-Location '$webAppDir'; `$env:VITE_API_BASE_URL='$ApiUrl'; npm run dev -- --host localhost --port $FrontendPort --strictPort"

Write-Host "Starting API window..."
Start-Process powershell -WorkingDirectory $repoRoot -ArgumentList @(
    "-NoExit",
    "-ExecutionPolicy", "Bypass",
    "-Command", $apiCommand
) | Out-Null

Write-Host "Starting Web window..."
Start-Process powershell -WorkingDirectory $webAppDir -ArgumentList @(
    "-NoExit",
    "-ExecutionPolicy", "Bypass",
    "-Command", $webCommand
) | Out-Null

if (-not (Wait-TcpPortOpen -TargetHost $apiHost -Port $apiPort -TimeoutSec 45)) {
    throw "API did not start on $ApiUrl within the expected time."
}

if (-not (Wait-TcpPortOpen -TargetHost "127.0.0.1" -Port $FrontendPort -TimeoutSec 45)) {
    throw "Frontend did not start on http://localhost:$FrontendPort within the expected time."
}

Write-Host ""
Write-Host "Web stack started."
Write-Host "API URL: $ApiUrl"
Write-Host "Frontend URL: http://localhost:$FrontendPort"
Write-Host ""
Write-Host "Tips:"
Write-Host "  - Stop servers by closing the two spawned PowerShell windows."
Write-Host "  - Use -SkipDocker if PostgreSQL is already running."
