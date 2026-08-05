# start.ps1 - Launches the Config System backend (.NET API) and frontend (React/Vite)
# Usage:  ./start.ps1

$ErrorActionPreference = "Continue"  # Don't stop on errors, continue execution
$root = $PSScriptRoot

$backend = Join-Path $root "backend\ConfigSystem.Api"
$frontend = Join-Path $root "frontend"

$Host.UI.RawUI.WindowTitle = "Config System"

Write-Host "Stopping any existing instances..." -ForegroundColor Yellow

# Kill any existing dotnet processes (backend)
$dotnetProcesses = Get-Process dotnet -ErrorAction SilentlyContinue
if ($dotnetProcesses) {
    foreach ($process in $dotnetProcesses) {
        try {
            Stop-Process -Id $process.Id -Force
            Write-Host "  Stopped dotnet process (PID: $($process.Id))" -ForegroundColor Gray
        } catch {
            Write-Host "  Warning: Could not stop dotnet process (PID: $($process.Id))" -ForegroundColor DarkYellow
        }
    }
} else {
    Write-Host "  No dotnet processes running" -ForegroundColor Gray
}

# Kill any existing node/npm processes (frontend on port 5173)
$nodeProcesses = Get-Process node -ErrorAction SilentlyContinue
if ($nodeProcesses) {
    foreach ($process in $nodeProcesses) {
        try {
            Stop-Process -Id $process.Id -Force
            Write-Host "  Stopped node process (PID: $($process.Id))" -ForegroundColor Gray
        } catch {
            Write-Host "  Warning: Could not stop node process (PID: $($process.Id))" -ForegroundColor DarkYellow
        }
    }
} else {
    Write-Host "  No node processes running" -ForegroundColor Gray
}

# Longer delay to ensure ports are released
Write-Host "Waiting for ports to release..." -ForegroundColor Gray
Start-Sleep -Seconds 1

Write-Host "Starting Utility Configuration System..." -ForegroundColor Cyan

# Install frontend dependencies on first run
if (-not (Test-Path (Join-Path $frontend "node_modules"))) {
    Write-Host "Installing frontend dependencies..." -ForegroundColor Yellow
    Push-Location $frontend
    npm install
    Pop-Location
}

# Verify backend path exists
if (-not (Test-Path $backend)) {
    Write-Host "ERROR: Backend path not found: $backend" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path (Join-Path $backend "ConfigSystem.Api.csproj"))) {
    Write-Host "ERROR: Project file not found: $backend\ConfigSystem.Api.csproj" -ForegroundColor Red
    exit 1
}

# Backend -> http://localhost:5198 (Swagger at /swagger)
Write-Host "Backend  : http://localhost:5198/swagger" -ForegroundColor Green
Write-Host "Starting backend from: $backend" -ForegroundColor Gray
$backendCmd = "cd `"$backend`"; `$Host.UI.RawUI.WindowTitle = 'Config System - Backend'; dotnet run 2>&1"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $backendCmd

# Frontend -> http://localhost:5173
Write-Host "Frontend : http://localhost:5173" -ForegroundColor Green
Write-Host "Starting frontend from: $frontend" -ForegroundColor Gray
$frontendCmd = "cd `"$frontend`"; `$Host.UI.RawUI.WindowTitle = 'Config System - Frontend'; npm run dev 2>&1"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $frontendCmd

# Give services time to start
Write-Host "Waiting for services to start..." -ForegroundColor Gray
Start-Sleep -Seconds 3

Write-Host "Both started in separate PowerShell windows. Close those windows to stop." -ForegroundColor Cyan
Write-Host "If backend didn't start, check the backend window for errors." -ForegroundColor Yellow
