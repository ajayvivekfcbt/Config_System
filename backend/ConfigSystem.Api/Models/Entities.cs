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
