-- Create databases
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE [name] = N'ConfigSystem')
BEGIN
    CREATE DATABASE ConfigSystem;
END

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE [name] = N'ConfigSystem_Dev')
BEGIN
    CREATE DATABASE ConfigSystem_Dev;
END

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE [name] = N'ConfigSystem_Fcb')
BEGIN
    CREATE DATABASE ConfigSystem_Fcb;
END

-- Switch to ConfigSystem database and set up
USE ConfigSystem;

-- Grant permissions to sa user
ALTER AUTHORIZATION ON DATABASE::ConfigSystem TO sa;

USE ConfigSystem_Dev;
ALTER AUTHORIZATION ON DATABASE::ConfigSystem_Dev TO sa;

USE ConfigSystem_Fcb;
ALTER AUTHORIZATION ON DATABASE::ConfigSystem_Fcb TO sa;
