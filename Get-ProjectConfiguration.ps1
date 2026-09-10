<#
.SYNOPSIS
    Retrieves GoAnywhere project configuration parameters and values for a specific environment.

.DESCRIPTION
    Fetches configuration data from the ConfigSystem API for a given project and environment.
    Returns parameters in a structured format suitable for XML processing and execution.

.PARAMETER ProjectId
    The numeric ID of the GoAnywhere project (e.g., 113 for mRDCgetCardinal).

.PARAMETER ProjectName
    The name of the GoAnywhere project (alternative to ProjectId).
    If ProjectName is provided, it will look up the ID first.

.PARAMETER Environment
    The target environment (e.g. DATO, DATI, DATU, DATV, DATN, FCB). The valid list is
    defined by the app settings file (GoAnywhere:Environments) and fetched from the API.

.PARAMETER ApiBase
    The base URL for the ConfigSystem API. Defaults to the CONFIGSYSTEM_API_BASE
    environment variable (set at install time to the promoted path), or
    http://localhost:5198/api for local development when that variable is not set.

.PARAMETER Format
    Output format: 'Table' (default), 'Json', 'Csv', or 'Object'

.PARAMETER IncludeSensitive
    If specified, includes sensitive fields (passwords, keys) in output.
    By default, sensitive values are masked.

.EXAMPLE
    # Get configuration for mRDCgetCardinal in DATO environment
    .\Get-ProjectConfiguration.ps1 -ProjectId 113 -Environment DATO

.EXAMPLE
    # Get configuration by project name
    .\Get-ProjectConfiguration.ps1 -ProjectName "mRDCgetCardinal" -Environment DATO

.EXAMPLE
    # Get configuration and output as JSON
    .\Get-ProjectConfiguration.ps1 -ProjectId 120 -Environment DATO -Format Json

.EXAMPLE
    # Get configuration for P2Go Posting file in all environments
    foreach ($env in @('DATO', 'DATI', 'DATU', 'DATV', 'DATN', 'FCB')) {
        .\Get-ProjectConfiguration.ps1 -ProjectName "P2Go Posting file" -Environment $env
    }
#>

param(
    [Parameter(ParameterSetName = 'ById', Mandatory = $true)]
    [int]$ProjectId,

    [Parameter(ParameterSetName = 'ByName', Mandatory = $true)]
    [string]$ProjectName,

    [Parameter(Mandatory = $true)]
    [string]$Environment,

    # Defaults to the installed (promoted) API path via CONFIGSYSTEM_API_BASE, else local dev.
    [string]$ApiBase = $(if ($env:CONFIGSYSTEM_API_BASE) { $env:CONFIGSYSTEM_API_BASE } else { "http://localhost:5198/api" }),

    [string]$ApiKey = $env:CONFIGSYSTEM_API_KEY,

    [ValidateSet('Table', 'Json', 'Csv', 'Object')]
    [string]$Format = 'Table',

    [switch]$IncludeSensitive
)

# Internal callers reach the read-only config endpoints without any credential. An
# optional API key is sent only when supplied, for callers outside the trusted network.
$RequestHeaders = @{}
if ($ApiKey) { $RequestHeaders['X-Api-Key'] = $ApiKey }

# The valid environment list lives in the app settings file (GoAnywhere:Environments) and
# is served by the API, so it never has to be hardcoded here. Falls back to a built-in
# list only when the API is unreachable.
function Get-ValidEnvironment {
    param([string]$ApiBase, [hashtable]$Headers)
    try {
        return @(Invoke-RestMethod -Uri "$ApiBase/goanywhere/environments" -Headers $Headers -ErrorAction Stop)
    }
    catch {
        Write-Verbose "Could not fetch environments from API ($_). Using built-in fallback list."
        return @('DATO', 'DATI', 'DATU', 'DATV', 'DATN', 'FCB')
    }
}

$validEnvironments = Get-ValidEnvironment -ApiBase $ApiBase -Headers $RequestHeaders
if ($Environment -notin $validEnvironments) {
    Write-Error "Environment '$Environment' is not valid. Configured environments: $($validEnvironments -join ', ')"
    exit 1
}

# Helper function to resolve project ID by name
function Get-ProjectIdByName {
    param([string]$Name, [string]$ApiBase, [hashtable]$Headers)
    
    try {
        $projects = Invoke-RestMethod -Uri "$ApiBase/goanywhere/projects" -Headers $Headers -ErrorAction Stop
        $project = $projects | Where-Object { $_.name -eq $Name } | Select-Object -First 1
        
        if ($project) {
            return $project.id
        } else {
            Write-Error "Project '$Name' not found. Available projects:`n$($projects | Select-Object -ExpandProperty name | Sort-Object)"
            return $null
        }
    }
    catch {
        Write-Error "Failed to fetch projects: $_"
        return $null
    }
}

# Resolve project ID if using project name
if ($PSCmdlet.ParameterSetName -eq 'ByName') {
    $ProjectId = Get-ProjectIdByName -Name $ProjectName -ApiBase $ApiBase -Headers $RequestHeaders
    if (-not $ProjectId) { exit 1 }
}

# Fetch configurations from API
try {
    Write-Verbose "Fetching configurations for ProjectId=$ProjectId, Environment=$Environment..."
    
    $configs = Invoke-RestMethod -Uri "$ApiBase/goanywhere/configs?projectId=$ProjectId&environment=$Environment" -Headers $RequestHeaders -ErrorAction Stop
    
    if (-not $configs -or $configs.Count -eq 0) {
        Write-Warning "No configurations found for ProjectId=$ProjectId in environment $Environment"
        exit 0
    }
    
    Write-Verbose "Retrieved $($configs.Count) configurations"
    
    # Mask sensitive values if not requested
    if (-not $IncludeSensitive) {
        $configs | ForEach-Object {
            if ($_.isSensitive) {
                $_.configValue = "***MASKED***"
            }
        }
    }
    
    # Format and output results
    switch ($Format) {
        'Json' {
            $configs | ConvertTo-Json -Depth 10 | Write-Output
        }
        'Csv' {
            $configs | ConvertTo-Csv -NoTypeInformation | Write-Output
        }
        'Object' {
            $configs | Write-Output
        }
        'Table' {
            $configs | Select-Object -Property @(
                'id',
                'configKey',
                'configValue',
                'isRequired',
                'isSensitive',
                'description'
            ) | Format-Table -AutoSize
        }
    }
    
    # Print summary
    Write-Host "`n========== Configuration Summary ==========" -ForegroundColor Green
    Write-Host "Project ID       : $ProjectId"
    Write-Host "Environment      : $Environment"
    Write-Host "Total Parameters : $($configs.Count)"
    
    $requiredCount = ($configs | Where-Object { $_.isRequired }).Count
    $sensitiveCount = ($configs | Where-Object { $_.isSensitive }).Count
    
    Write-Host "Required         : $requiredCount"
    Write-Host "Sensitive        : $sensitiveCount"
    Write-Host "=========================================`n"
    
}
catch [System.Net.Http.HttpRequestException] {
    Write-Error "Failed to connect to API at $ApiBase. Is the backend running?"
    exit 1
}
catch {
    Write-Error "Error fetching configurations: $_"
    exit 1
}
