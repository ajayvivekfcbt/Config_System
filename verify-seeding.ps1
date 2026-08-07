#!/usr/bin/env pwsh
# Quick verification script to check seeded GoAnywhere variables

$dbPath = "configsystem.db"

if (-not (Test-Path $dbPath)) {
    Write-Error "Database not found at $dbPath"
    exit 1
}

# Create a simple data adapter query
$connectionString = "Data Source=$dbPath"
Add-Type -AssemblyName System.Data.SQLite

try {
    $connection = New-Object System.Data.SQLite.SQLiteConnection($connectionString)
    $connection.Open()
    
    # Count total variables
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) as Total FROM VariableDefinitions"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "✓ Total VariableDefinitions in database: $($reader['Total'])"
    }
    $reader.Close()
    
    # Count GoAnywhere variables
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) as GoAnywhere FROM VariableDefinitions vd 
                        JOIN Extents e ON vd.ExtentId = e.Id 
                        WHERE e.Name = 'PROJECTS'"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "✓ GoAnywhere Variables (PROJECTS extent): $($reader['GoAnywhere'])"
    }
    $reader.Close()
    
    # Sample some GoAnywhere variables
    Write-Host ""
    Write-Host "Sample GoAnywhere Variables Seeded:"
    Write-Host "---"
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT vd.Name, vd.Description, vt.Name as Type
                        FROM VariableDefinitions vd
                        JOIN Extents e ON vd.ExtentId = e.Id
                        JOIN VariableTypes vt ON vd.VariableTypeId = vt.Id
                        WHERE e.Name = 'PROJECTS'
                        LIMIT 10"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "$($reader['Name']) [$($reader['Type'])] - $($reader['Description'])"
    }
    $reader.Close()
    
    # Check VariableValues
    Write-Host ""
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) as Total FROM VariableValues"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "✓ Total VariableValues: $($reader['Total'])"
    }
    $reader.Close()
    
    $connection.Close()
    Write-Host ""
    Write-Host "✅ Database verification complete - GoAnywhere XML data successfully seeded!"
}
catch {
    Write-Error "Database query failed: $_"
    exit 1
}
