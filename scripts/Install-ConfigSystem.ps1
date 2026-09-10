[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path (Join-Path $_ "api") })]
    [string]$PackageRoot,

    [Parameter(Mandatory)]
    [string]$ApiInstallPath,

    [Parameter(Mandatory)]
    [string]$FrontendInstallPath,

    [Parameter(Mandatory)]
    [string]$ApiServiceName,

    # Promoted API base URL for the installed host. When supplied it is persisted as the
    # machine-level CONFIGSYSTEM_API_BASE variable so client scripts target the right host.
    [string]$ApiBaseUrl
)

$ErrorActionPreference = "Stop"

function Copy-InstallDirectory {
    param(
        [Parameter(Mandatory)] [string]$Source,
        [Parameter(Mandatory)] [string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    robocopy $Source $Destination /MIR /R:2 /W:2 /XD "dp-keys" /XF "appsettings.Production.json" "*.db" "*.db-shm" "*.db-wal"
    if ($LASTEXITCODE -gt 7) {
        throw "Failed to install files from '$Source' to '$Destination' (robocopy exit code $LASTEXITCODE)."
    }
}

$apiSource = Join-Path $PackageRoot "api"
$frontendSource = Join-Path $PackageRoot "frontend"
if (-not (Test-Path $frontendSource)) {
    throw "Frontend package was not found at '$frontendSource'."
}

$service = Get-Service -Name $ApiServiceName -ErrorAction Stop
if ($service.Status -ne "Stopped") {
    Stop-Service -Name $ApiServiceName -Force
    $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(60))
}

Copy-InstallDirectory -Source $apiSource -Destination $ApiInstallPath
Copy-InstallDirectory -Source $frontendSource -Destination $FrontendInstallPath

Start-Service -Name $ApiServiceName
(Get-Service -Name $ApiServiceName).WaitForStatus("Running", [TimeSpan]::FromSeconds(60))

if ($ApiBaseUrl) {
    [Environment]::SetEnvironmentVariable("CONFIGSYSTEM_API_BASE", $ApiBaseUrl, "Machine")
}