using System.ComponentModel.DataAnnotations;

namespace ConfigSystem.Api.Models;

/// <summary>
/// UTCFGVTP - Configuration Variable Type Definitions.
/// Defines a data type for variables and the validation procedure used for it.
/// </summary>
public class VariableType
{
    public int Id { get; set; }                       // VTP_ID
    [MaxLength(20)] public string Name { get; set; } = "";        // VTP_NAME
    [MaxLength(50)] public string Description { get; set; } = ""; // VTP_DESC
    [MaxLength(128)] public string ValidationProcedure { get; set; } = ""; // VTP_PROC
    [MaxLength(2048)] public string Information { get; set; } = "";         // VTP_INFO

    public ICollection<VariableDefinition> VariableDefinitions { get; set; } = new List<VariableDefinition>();
}

/// <summary>UTCFGSRM - Configuration Scope Resolution Methods.</summary>
public class ScopeResolutionMethod
{
    public int Id { get; set; }                       // SRM_ID
    [MaxLength(20)] public string Name { get; set; } = "";        // SRM_NAME
    [MaxLength(50)] public string Description { get; set; } = ""; // SRM_DESC
    [MaxLength(2048)] public string Information { get; set; } = "";         // SRM_INFO

    public ICollection<Scope> Scopes { get; set; } = new List<Scope>();
}

/// <summary>UTCFGSRV - Configuration Server Definitions.</summary>
public class Server
{
    public int Id { get; set; }                       // SRV_ID
    [MaxLength(20)] public string Name { get; set; } = "";        // SRV_NAME
    [MaxLength(50)] public string Description { get; set; } = ""; // SRV_DESC
    [MaxLength(128)] public string MatchValue { get; set; } = ""; // SRV_MATCH
    [MaxLength(2048)] public string Information { get; set; } = "";         // SRV_INFO

    public ICollection<Scope> Scopes { get; set; } = new List<Scope>();
}

/// <summary>UTCFGCTX - Configuration Context Definitions.</summary>
public class Context
{
    public int Id { get; set; }                       // CTX_ID
    [MaxLength(20)] public string Name { get; set; } = "";        // CTX_NAME
    [MaxLength(50)] public string Description { get; set; } = ""; // CTX_DESC
    public bool IsServer { get; set; }                // CTX_IS_SRV
    [MaxLength(2048)] public string Information { get; set; } = "";         // CTX_INFO

    public ICollection<Extent> Extents { get; set; } = new List<Extent>();
}

/// <summary>UTCFGXTN - Configuration Extent Definitions.</summary>
public class Extent
{
    public int Id { get; set; }                       // XTN_ID
    public int ContextId { get; set; }                // CTX_ID
    public Context? Context { get; set; }
    [MaxLength(20)] public string Name { get; set; } = "";        // XTN_NAME
    [MaxLength(50)] public string Description { get; set; } = ""; // XTN_DESC
    [MaxLength(2048)] public string Information { get; set; } = "";         // XTN_INFO

    public ICollection<VariableDefinition> VariableDefinitions { get; set; } = new List<VariableDefinition>();
}

/// <summary>UTCFGSCP - Configuration Scope Definitions.</summary>
public class Scope
{
    public int Id { get; set; }                       // SCP_ID
    public int ServerId { get; set; }                 // SRV_ID
    public Server? Server { get; set; }
    public int ScopeResolutionMethodId { get; set; }  // SRM_ID
    public ScopeResolutionMethod? ScopeResolutionMethod { get; set; }
    [MaxLength(20)] public string Name { get; set; } = "";        // SCP_NAME
    [MaxLength(50)] public string Description { get; set; } = ""; // SCP_DESC
    public bool IsServer { get; set; }                // SCP_IS_SRV
    [MaxLength(128)] public string MatchValue { get; set; } = ""; // SCP_MATCH
    [MaxLength(2048)] public string Information { get; set; } = "";         // SCP_INFO

    public ICollection<VariableValue> VariableValues { get; set; } = new List<VariableValue>();
}

/// <summary>UTCFGVDF - Configuration Variable Definitions.</summary>
public class VariableDefinition
{
    public int Id { get; set; }                       // VDF_ID
    public int ExtentId { get; set; }                 // XTN_ID
    public Extent? Extent { get; set; }
    public int VariableTypeId { get; set; }           // VTP_ID
    public VariableType? VariableType { get; set; }
    [MaxLength(50)] public string Name { get; set; } = "";        // VDF_NAME
    [MaxLength(50)] public string Description { get; set; } = ""; // VDF_DESC
    public bool ValuesAreRestricted { get; set; }     // VDF_RSTRCT
    [MaxLength(2048)] public string Information { get; set; } = "";         // VDF_INFO

    public ICollection<ValidValue> ValidValues { get; set; } = new List<ValidValue>();
    public ICollection<VariableValue> VariableValues { get; set; } = new List<VariableValue>();
}

/// <summary>UTCFGVVL - Configuration Variable Valid Values.</summary>
public class ValidValue
{
    public int Id { get; set; }                       // VVL_ID
    public int VariableDefinitionId { get; set; }     // VDF_ID
    public VariableDefinition? VariableDefinition { get; set; }
    [MaxLength(128)] public string Value { get; set; } = "";      // VVL_VALUE
    [MaxLength(50)] public string Description { get; set; } = ""; // VVL_DESC
    [MaxLength(2048)] public string Information { get; set; } = "";         // VVL_INFO
}

/// <summary>UTCFGVAL - Configuration Variable Values.</summary>
public class VariableValue
{
    public int Id { get; set; }                       // VAL_ID
    public int VariableDefinitionId { get; set; }     // VDF_ID
    public VariableDefinition? VariableDefinition { get; set; }
    public int ScopeId { get; set; }                  // SCP_ID
    public Scope? Scope { get; set; }
    [MaxLength(128)] public string Value { get; set; } = "";      // VAL_VARVAL
}

// ============================================================================
// GoAnywhere Configuration Management Models
// ============================================================================

/// <summary>
/// GAPROJECT - GoAnywhere Project definitions
/// References all GoAnywhere projects managed in the system
/// </summary>
public class GoAnywhereProject
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
    [MaxLength(100)] public string? ProjectPath { get; set; }
    public int? ContextId { get; set; }
    public int? ExtentId { get; set; }
    [MaxLength(50)] public string? ExtentName { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    public ICollection<GoAnywhereConfig> Configurations { get; set; } = new List<GoAnywhereConfig>();
}

/// <summary>
/// GACONFIG - GoAnywhere Configuration values
/// Stores configuration parameters for each GoAnywhere project by environment
/// </summary>
public class GoAnywhereConfig
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public GoAnywhereProject? Project { get; set; }
    [MaxLength(20)] public string Environment { get; set; } = "";  // DEV, TEST, PROD
    [MaxLength(100)] public string ConfigKey { get; set; } = "";   // ProjectPath, GAProject, ParameterFile, etc.
    [MaxLength(1000)] public string? ConfigValue { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSensitive { get; set; }  // Flag for passwords/credentials
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// GAAUDIT - Audit trail for GoAnywhere parameter value changes and deletions.
/// </summary>
public class GoAnywhereAuditLog
{
    public int Id { get; set; }
    public int? ConfigId { get; set; }
    public int ProjectId { get; set; }
    [MaxLength(100)] public string ProjectName { get; set; } = "";
    [MaxLength(20)] public string Environment { get; set; } = "";
    [MaxLength(100)] public string ConfigKey { get; set; } = "";
    [MaxLength(1000)] public string? OldValue { get; set; }
    [MaxLength(1000)] public string? NewValue { get; set; }
    [MaxLength(30)] public string Action { get; set; } = "";
    [MaxLength(64)] public string ChangedBy { get; set; } = "unknown";
    public bool IsSensitive { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}

// ============================================================================
// IBM i Configuration System Audit Logging
// ============================================================================

/// <summary>
/// IBMIAUDIT - Audit trail for IBM i configuration value changes and deletions.
/// Tracks all modifications to IBM i Configuration System (UTCFG*) parameters and values.
/// </summary>
public class IBMiAuditLog
{
    public int Id { get; set; }
    public int? ConfigId { get; set; }  // VariableValue Id if applicable
    public int? VariableDefinitionId { get; set; }  // VDF_ID for tracking parameter definitions
    public int? ScopeId { get; set; }  // SCP_ID for scope context
    [MaxLength(100)] public string ScopeName { get; set; } = "";
    [MaxLength(100)] public string VariableDefName { get; set; } = "";  // VDF_NAME
    [MaxLength(100)] public string ExtentName { get; set; } = "";  // XTN_NAME (project context)
    [MaxLength(20)] public string Environment { get; set; } = "";  // DEV, TEST, PROD if applicable
    [MaxLength(1000)] public string? OldValue { get; set; }
    [MaxLength(1000)] public string? NewValue { get; set; }
    [MaxLength(30)] public string Action { get; set; } = "";  // CREATE, UPDATE, DELETE
    [MaxLength(64)] public string ChangedBy { get; set; } = "unknown";
    public bool IsSensitive { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
