-- Repair GAAUDIT.Id after SQL Server Import/Export Wizard import.
-- Run once in ConfigSystem_Dev and ConfigSystem_Fcb.
-- Take a backup first. Existing audit rows and IDs are preserved.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.GAAUDIT', N'U') IS NULL
    THROW 50001, 'dbo.GAAUDIT does not exist in the selected database.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.identity_columns
    WHERE object_id = OBJECT_ID(N'dbo.GAAUDIT')
      AND name = N'Id'
)
BEGIN
    EXEC sys.sp_rename N'dbo.GAAUDIT', N'GAAUDIT_ImportBackup';

    CREATE TABLE dbo.GAAUDIT
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GAAUDIT PRIMARY KEY,
        ConfigId INT NULL,
        ProjectId INT NOT NULL,
        ProjectName NVARCHAR(100) NOT NULL,
        Environment NVARCHAR(20) NOT NULL,
        ConfigKey NVARCHAR(100) NOT NULL,
        OldValue NVARCHAR(1000) NULL,
        NewValue NVARCHAR(1000) NULL,
        Action NVARCHAR(30) NOT NULL,
        ChangedBy NVARCHAR(64) NOT NULL,
        IsSensitive CHAR(1) NOT NULL,
        ChangedAtUtc DATETIME2(7) NOT NULL
    );

    SET IDENTITY_INSERT dbo.GAAUDIT ON;
    INSERT INTO dbo.GAAUDIT
    (
        Id, ConfigId, ProjectId, ProjectName, Environment, ConfigKey,
        OldValue, NewValue, Action, ChangedBy, IsSensitive, ChangedAtUtc
    )
    SELECT
        Id, ConfigId, ProjectId, ProjectName, Environment, ConfigKey,
        OldValue, NewValue, Action, ChangedBy, IsSensitive, ChangedAtUtc
    FROM dbo.GAAUDIT_ImportBackup;
    SET IDENTITY_INSERT dbo.GAAUDIT OFF;

    CREATE INDEX IX_GAAUDIT_ChangedAtUtc
        ON dbo.GAAUDIT (ChangedAtUtc);
    CREATE INDEX IX_GAAUDIT_ProjectName_Environment_ConfigKey
        ON dbo.GAAUDIT (ProjectName, Environment, ConfigKey);

    DROP TABLE dbo.GAAUDIT_ImportBackup;
END;

COMMIT TRANSACTION;

SELECT c.name AS ColumnName, t.name AS SqlType, c.is_identity AS IsIdentity
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.GAAUDIT')
  AND c.name IN (N'Id', N'IsSensitive', N'ChangedAtUtc');
