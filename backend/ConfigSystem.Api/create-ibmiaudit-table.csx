#!/usr/bin/env dotnet-script
// Script to create IBMIAUDIT table in SQLite databases

using System;
using System.Data.SQLite;
using System.IO;

string baseDir = AppContext.BaseDirectory;
string[] databases = { "configsystem.db", "configsystem-fcb.db" };

string createTableSql = @"
CREATE TABLE IF NOT EXISTS IBMIAUDIT (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigId INTEGER,
    VariableDefinitionId INTEGER,
    ScopeId INTEGER,
    ScopeName TEXT NOT NULL CHECK(length(ScopeName) <= 100),
    VariableDefName TEXT NOT NULL CHECK(length(VariableDefName) <= 100),
    ExtentName TEXT NOT NULL CHECK(length(ExtentName) <= 100),
    Environment TEXT NOT NULL CHECK(length(Environment) <= 20),
    OldValue TEXT CHECK(length(OldValue) <= 1000),
    NewValue TEXT CHECK(length(NewValue) <= 1000),
    Action TEXT NOT NULL CHECK(length(Action) <= 30),
    ChangedBy TEXT NOT NULL CHECK(length(ChangedBy) <= 64),
    IsSensitive INTEGER NOT NULL,
    ChangedAtUtc TEXT NOT NULL
);
";

foreach (string dbFile in databases)
{
    string dbPath = Path.Combine(baseDir, dbFile);
    
    if (!File.Exists(dbPath))
    {
        Console.WriteLine($"Database file not found: {dbPath}");
        continue;
    }
    
    try
    {
        string connectionString = $"Data Source={dbPath};Version=3;";
        using (SQLiteConnection conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            Console.WriteLine($"Connected to {dbFile}");
            
            using (SQLiteCommand cmd = new SQLiteCommand(createTableSql, conn))
            {
                cmd.ExecuteNonQuery();
                Console.WriteLine($"✓ IBMIAUDIT table created/verified in {dbFile}");
            }
            
            conn.Close();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error processing {dbFile}: {ex.Message}");
    }
}

Console.WriteLine("Done!");
