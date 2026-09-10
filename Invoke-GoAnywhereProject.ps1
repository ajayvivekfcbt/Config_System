<#
.SYNOPSIS
    Executes a GoAnywhere project with configurations from the ConfigSystem API.

.DESCRIPTION
    This script retrieves configuration parameters for a GoAnywhere project from the
    ConfigSystem API, then invokes the project on a GoAnywhere server using the
    configured parameters and optional additional variables.

.PARAMETER ProjectName
    The name of the GoAnywhere project to execute (required).

.PARAMETER Environment
    The environment to retrieve configurations for (required).
    Valid values come from the app settings file (GoAnywhere:Environments), served by the API.

.PARAMETER GoAnywhereUrl
    The base URL of the GoAnywhere server (required).

.PARAMETER ApiKey
    API key for GoAnywhere authentication (required). Pass it from a secure
    secret store or environment variable; it is never stored in this script.

.PARAMETER ConfigApiUrl
    The base URL of the ConfigSystem API (default: http://localhost:5198/api).

.PARAMETER ProjectPath
    The folder path where the project is located on GoAnywhere server (e.g., /dev/Ajay).
    If not specified, projects are searched in the root folder.

.PARAMETER AdditionalVariables
    Hashtable of additional variables to pass to GoAnywhere.

.PARAMETER SkipCertificateCheck
    Skip SSL certificate validation (useful for self-signed certificates).
    Works with PowerShell 6.0 and later.

.PARAMETER Verbose
    Enable verbose output for debugging.

.EXAMPLE
    .\Invoke-GoAnywhereProject.ps1 -ProjectName "APClearedChecks" -Environment DATO `
        -GoAnywhereUrl "http://goanywhere.company.com:8080" -ApiKey "your-api-key-here" `
        -ProjectPath "/dev/Ajay"

.EXAMPLE
    .\Invoke-GoAnywhereProject.ps1 -ProjectName "TestAPI" -Environment DATO `
        -GoAnywhereUrl "https://GOANYDEV.develop.fcbt:8001/goanywhere/rest/gacmd/v1/projects" `
        -ApiKey $env:GOANYWHERE_API_KEY `
        -ProjectPath "/dev/Ajay" -SkipCertificateCheck
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$ProjectName,

    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$Environment,

    [Parameter(Mandatory=$false)]
    [ValidateNotNullOrEmpty()]
    [string]$GoAnywhereUrl = "https://GOANYDEV.develop.fcbt:8001/goanywhere/rest/gacmd/v1/projects",

    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$ApiKey,

    [Parameter(Mandatory=$false)]
    [string]$ConfigApiUrl = "http://localhost:5000/api",

    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = "/dev/Ajay",

    [Parameter(Mandatory=$false)]
    [hashtable]$AdditionalVariables = @{},

    [Parameter(Mandatory=$false)]
    [switch]$SkipCertificateCheck
)

$ErrorActionPreference = "Stop"

# Headers required by the ConfigSystem API gate
$script:ConfigApiHeaders = @{
    "X-App-Key"  = "config-system-web-app"
    "X-User-Id"  = "ps-script"
}

# The valid environment list lives in the app settings file (GoAnywhere:Environments) and
# is served by the API, so it is never hardcoded here. Falls back to a built-in list only
# when the API is unreachable.
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

$validEnvironments = Get-ValidEnvironment -ApiBase $ConfigApiUrl -Headers $script:ConfigApiHeaders
if ($Environment -notin $validEnvironments) {
    Write-Error "Environment '$Environment' is not valid. Configured environments: $($validEnvironments -join ', ')"
    exit 1
}

# Handle certificate validation for older PowerShell versions
if ($SkipCertificateCheck) {
    if ($PSVersionTable.PSVersion.Major -lt 6) {
        # For Windows PowerShell 5.1 and earlier
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {
            param($sender, $certificate, $chain, $policy)
            return $true
        }
    }
}

# ============================================================================
# Helper Functions
# ============================================================================

function Write-VerboseLog {
    param([string]$Message)
    Write-Verbose "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Message"
}

function Test-ApiConnection {
    param([string]$Url)
    
    try {
        $params = @{
            Uri     = "$Url/goanywhere/summary"
            Method  = "Get"
            Headers = $script:ConfigApiHeaders
            ErrorAction = "Stop"
        }
        if ($SkipCertificateCheck -and $PSVersionTable.PSVersion.Major -ge 6) {
            $params["SkipCertificateCheck"] = $true
        }
        $result = Invoke-RestMethod @params
        return $true
    }
    catch {
        Write-Error "Failed to connect to API at $Url"
        return $false
    }
}

function Get-ProjectId {
    param([string]$Name)
    
    try {
        $params = @{
            Uri     = "$ConfigApiUrl/goanywhere/projects"
            Method  = "Get"
            Headers = $script:ConfigApiHeaders
        }
        if ($SkipCertificateCheck -and $PSVersionTable.PSVersion.Major -ge 6) {
            $params["SkipCertificateCheck"] = $true
        }
        
        $response = Invoke-RestMethod @params
        $projects = if ($response -is [array]) { $response } else { $response.value }
        $project = $projects | Where-Object { $_.name -eq $Name }
        
        if (-not $project) {
            throw "Project '$Name' not found in ConfigSystem"
        }
        
        return $project.id
    }
    catch {
        Write-Error "Error retrieving project list: $_"
        throw
    }
}

function Get-ProjectConfigurations {
    param(
        [int]$ProjectId,
        [string]$Env
    )
    
    try {
        $params = @{
            Uri     = "$ConfigApiUrl/goanywhere/configs?projectId=$ProjectId&environment=$Env"
            Method  = "Get"
            Headers = $script:ConfigApiHeaders
        }
        if ($SkipCertificateCheck -and $PSVersionTable.PSVersion.Major -ge 6) {
            $params["SkipCertificateCheck"] = $true
        }
        $configs = Invoke-RestMethod @params
        return $configs
    }
    catch {
        Write-Error "Error retrieving configurations: $_"
        throw
    }
}

function Build-GoAnywhereVariables {
    param(
        [object[]]$ConfigData,
        [hashtable]$AdditionalVars
    )
    
    $variables = @{}
    
    foreach ($config in $ConfigData) {
        $variables[$config.configKey] = $config.configValue
    }
    
    foreach ($key in $AdditionalVars.Keys) {
        $variables[$key] = $AdditionalVars[$key]
    }
    
    return $variables
}

function Invoke-GoAnywhereExecution {
    param(
        [string]$ProjectName,
        [hashtable]$Variables,
        [string]$GoAnywhereApiUrl,
        [string]$ApiKeyValue,
        [string]$ProjectPathValue = ""
    )
    
    try {
        # Construct full project path
        $fullProjectPath = if ($ProjectPathValue) {
            # Ensure project path starts with '/' and remove trailing slashes
            $path = $ProjectPathValue.TrimEnd('/')
            if (-not $path.StartsWith('/')) {
                $path = '/' + $path
            }
            "$path/$ProjectName"
        } else {
            $ProjectName
        }
        
        # Convert hashtable variables to array of key-value objects for GoAnywhere API
        $variablesArray = @()
        foreach ($key in $Variables.Keys) {
            $variablesArray += @{
                key   = $key
                value = $Variables[$key]
            }
        }
        
        # GoAnywhere expects parameters wrapped in 'runParameters'
        $runParams = @{
            project   = $fullProjectPath
            variables = $variablesArray
        }
        
        $body = @{
            runParameters = $runParams
        } | ConvertTo-Json -Depth 10
        
        # Construct the execute URL - intelligently handle different endpoint formats
        $executeUrl = if ($GoAnywhereApiUrl -match "/projects$") {
            # Already at projects endpoint, use as-is
            $GoAnywhereApiUrl
        } elseif ($GoAnywhereApiUrl -match "/execute$") {
            # Already has /execute suffix, use as-is
            $GoAnywhereApiUrl
        } else {
            # Legacy format, append /projects/execute
            "$GoAnywhereApiUrl/projects/execute"
        }
        
        $headers = @{
            "Authorization" = "Bearer $ApiKeyValue"
            "Content-Type"  = "application/json"
        }
        
        $params = @{
            Uri     = $executeUrl
            Method  = "Post"
            Body    = $body
            Headers = $headers
        }
        if ($SkipCertificateCheck -and $PSVersionTable.PSVersion.Major -ge 6) {
            $params["SkipCertificateCheck"] = $true
        }
        
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-Error "Failed to execute GoAnywhere project: $_"
        throw
    }
}

# ============================================================================
# Main Execution
# ============================================================================

try {
    # Use the provided ProjectName directly
    $effectiveProjectName = $ProjectName
    
    # Get Project ID
    $projectId = Get-ProjectId -Name $effectiveProjectName
    
    # Get Configurations
    $configs = Get-ProjectConfigurations -ProjectId $projectId -Env $Environment
    
    # Build Variables
    $variables = Build-GoAnywhereVariables -ConfigData $configs -AdditionalVars $AdditionalVariables
    
    # Execute
    $goAnywhereExecuteUrl = if ($GoAnywhereUrl -match "/gacmd/") {
        $GoAnywhereUrl
    } else {
        "$GoAnywhereUrl/api/projects/execute"
    }
    
    $result = Invoke-GoAnywhereExecution `
        -ProjectName $effectiveProjectName `
        -Variables $variables `
        -GoAnywhereApiUrl $goAnywhereExecuteUrl `
        -ApiKeyValue $ApiKey `
        -ProjectPathValue $ProjectPath
    
    # Display result
    if ($result -is [int]) {
        Write-Host "SUCCESS: Project executed (Execution ID: $result)" -ForegroundColor Green
    }
    elseif ($result.status -eq "success" -or $result.executionId) {
        Write-Host "SUCCESS: Project executed (ID: $($result.executionId))" -ForegroundColor Green
    }
    else {
        Write-Host $result -ForegroundColor Yellow
    }
}
catch {
    Write-Error $_
    exit 1
}
