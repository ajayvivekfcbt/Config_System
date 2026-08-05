using Microsoft.Data.Sqlite;

// Query the configsystem.db for GoAnywhere data
var connectionString = "Data Source=configsystem.db";
using var connection = new SqliteConnection(connectionString);
connection.Open();

Console.WriteLine("========== GoAnywhere Configuration Summary ==========\n");

// Count projects
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM GAPROJECT";
    var projectCount = (long)cmd.ExecuteScalar();
    Console.WriteLine($"✓ GoAnywhere Projects: {projectCount}");
}

// Count configurations
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM GACONFIG";
    var configCount = (long)cmd.ExecuteScalar();
    Console.WriteLine($"✓ GoAnywhere Configurations: {configCount}");
}

// Count by environment
Console.WriteLine("\nConfigurations by Environment:");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = @"
        SELECT ENVIRONMENT, COUNT(*) as Count 
        FROM GACONFIG 
        GROUP BY ENVIRONMENT 
        ORDER BY ENVIRONMENT";
    
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var env = reader.GetString(0);
        var count = reader.GetInt32(1);
        Console.WriteLine($"  {env}: {count} records");
    }
}

// Sample projects
Console.WriteLine("\nSample Projects:");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT Name, Description FROM GAPROJECT LIMIT 5";
    
    using var reader = cmd.ExecuteReader();
    int index = 1;
    while (reader.Read())
    {
        var name = reader.GetString(0);
        var desc = reader.GetString(1);
        Console.WriteLine($"  {index}. {name} - {desc}");
        index++;
    }
}

// Sample configurations for AgriLine
Console.WriteLine("\nSample Configurations (AgriLine DEV):");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = @"
        SELECT ConfigKey, ConfigValue, IsSensitive
        FROM GACONFIG
        WHERE ProjectId = (SELECT Id FROM GAPROJECT WHERE Name = 'AgriLine')
        AND ENVIRONMENT = 'DEV'
        ORDER BY ConfigKey
        LIMIT 8";
    
    using var reader = cmd.ExecuteReader();
    int index = 1;
    while (reader.Read())
    {
        var key = reader.GetString(0);
        var value = reader.GetString(1);
        var isSensitive = reader.GetBoolean(2);
        var displayValue = isSensitive ? "●●●●●●●●" : value;
        Console.WriteLine($"  {index}. {key}: {displayValue}");
        index++;
    }
}

Console.WriteLine("\n========== Seeding Complete! ==========\n");
