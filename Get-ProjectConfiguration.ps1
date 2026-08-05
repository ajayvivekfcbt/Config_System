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
    The target environment: DATO, DATI, DATU, DATV, DATN, or FCB.

.PARAMETER ApiBase
    The base URL for the ConfigSystem API. Defaults to http://localhost:5198/api

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
    [ValidateSet('DATO', 'DATI', 'DATU', 'DATV', 'DATN', 'FCB')]
    [string]$Environment,

    [string]$ApiBase = "http://localhost:5198/api",

    [ValidateSet('Table', 'Json', 'Csv', 'Object')]
    [string]$Format = 'Table',

    [switch]$IncludeSensitive
)

# Helper function to resolve project ID by name
function Get-ProjectIdByName {
    param([string]$Name, [string]$ApiBase)
    
    try {
        $projects = Invoke-RestMethod -Uri "$ApiBase/goanywhere/projects" -ErrorAction Stop
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
    $ProjectId = Get-ProjectIdByName -Name $ProjectName -ApiBase $ApiBase
    if (-not $ProjectId) { exit 1 }
}

# Fetch configurations from API
try {
    Write-Verbose "Fetching configurations for ProjectId=$ProjectId, Environment=$Environment..."
    
    $configs = Invoke-RestMethod -Uri "$ApiBase/goanywhere/configs?projectId=$ProjectId&environment=$Environment" -ErrorAction Stop
    
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
