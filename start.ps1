# start.ps1 - Launches the Config System backend (.NET API) and frontend (React/Vite)
# Usage:  ./start.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$backend = Join-Path $root "backend\ConfigSystem.Api"
$frontend = Join-Path $root "frontend"

$Host.UI.RawUI.WindowTitle = "Config System"

Write-Host "Starting Utility Configuration System..." -ForegroundColor Cyan

# Install frontend dependencies on first run
if (-not (Test-Path (Join-Path $frontend "node_modules"))) {
    Write-Host "Installing frontend dependencies..." -ForegroundColor Yellow
    Push-Location $frontend
    npm install
    Pop-Location
}

# Backend -> http://localhost:5198 (Swagger at /swagger)
Write-Host "Backend  : http://localhost:5198/swagger" -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle = 'Config System - Backend'; cd `"$backend`"; dotnet run"

# Frontend -> http://localhost:5173
Write-Host "Frontend : http://localhost:5173" -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle = 'Config System - Frontend'; cd `"$frontend`"; npm run dev"

Write-Host "Both started in separate PowerShell windows. Close those windows to stop." -ForegroundColor Cyan
