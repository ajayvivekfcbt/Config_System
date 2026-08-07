using ConfigSystem.Api.Models;
using System.Reflection;

namespace ConfigSystem.Api.Data;

/// <summary>
/// Seeds a representative configuration that mirrors the structure the original
/// RPG system maintained, so the API and UI are usable out of the box.
/// </summary>
public static class SeedData
{
    public static void EnsureSeeded(ConfigDbContext db)
    {
        db.Database.EnsureCreated();
        if (db.VariableTypes.Any()) return;

        var tString = new VariableType { Name = "STRING", Description = "Free text value", ValidationProcedure = "VALSTRING", Information = "Any string up to 128 chars" };
        var tInt = new VariableType { Name = "INTEGER", Description = "Whole number", ValidationProcedure = "VALINT", Information = "Signed integer" };
        var tBool = new VariableType { Name = "BOOLEAN", Description = "True/False flag", ValidationProcedure = "VALBOOL", Information = "Y or N" };
        db.VariableTypes.AddRange(tString, tInt, tBool);

        var mExact = new ScopeResolutionMethod { Name = "EXACT", Description = "Exact server match", Information = "Match server name exactly" };
        var mDefault = new ScopeResolutionMethod { Name = "DEFAULT", Description = "Fallback default", Information = "Applies when no exact scope matches" };
        db.ScopeResolutionMethods.AddRange(mExact, mDefault);

        var srvProd = new Server { Name = "PROD01", Description = "Production server 1", MatchValue = "PROD01", Information = "" };
        var srvDev = new Server { Name = "DEV01", Description = "Development server 1", MatchValue = "DEV01", Information = "" };
        db.Servers.AddRange(srvProd, srvDev);

        var ctxApp = new Context { Name = "APPLICATION", Description = "Application settings", IsServer = false, Information = "" };
        var ctxSys = new Context { Name = "SYSTEM", Description = "System settings", IsServer = true, Information = "" };
        var ctxGoAnywhere = new Context { Name = "GOANYWHRE", Description = "GoAnywhere integration", IsServer = false, Information = "" };
        db.Contexts.AddRange(ctxApp, ctxSys, ctxGoAnywhere);

        var xtnBilling = new Extent { Context = ctxApp, Name = "BILLING", Description = "Billing module", Information = "" };
        var xtnCore = new Extent { Context = ctxSys, Name = "CORE", Description = "Core system", Information = "" };
        var xtnGoAnywhere = new Extent { Context = ctxGoAnywhere, Name = "PROJECTS", Description = "GoAnywhere project variables", Information = "" };
        db.Extents.AddRange(xtnBilling, xtnCore, xtnGoAnywhere);

        var scpProd = new Scope { Server = srvProd, ScopeResolutionMethod = mExact, Name = "PROD-EXACT", Description = "Prod exact scope", IsServer = true, MatchValue = "PROD01", Information = "" };
        var scpDev = new Scope { Server = srvDev, ScopeResolutionMethod = mExact, Name = "DEV-EXACT", Description = "Dev exact scope", IsServer = true, MatchValue = "DEV01", Information = "" };
        var scpDefault = new Scope { Server = srvProd, ScopeResolutionMethod = mDefault, Name = "GLOBAL-DEFAULT", Description = "Global default scope", IsServer = false, MatchValue = "*", Information = "" };
        db.Scopes.AddRange(scpProd, scpDev, scpDefault);

        var vdfRetries = new VariableDefinition { Extent = xtnBilling, VariableType = tInt, Name = "MAX_RETRIES", Description = "Max billing retries", ValuesAreRestricted = false, Information = "" };
        var vdfMode = new VariableDefinition { Extent = xtnBilling, VariableType = tString, Name = "BILLING_MODE", Description = "Billing mode", ValuesAreRestricted = true, Information = "" };
        var vdfDebug = new VariableDefinition { Extent = xtnCore, VariableType = tBool, Name = "DEBUG_ENABLED", Description = "Enable debug logging", ValuesAreRestricted = true, Information = "" };
        db.VariableDefinitions.AddRange(vdfRetries, vdfMode, vdfDebug);

        db.ValidValues.AddRange(
            new ValidValue { VariableDefinition = vdfMode, Value = "REALTIME", Description = "Real time billing", Information = "" },
            new ValidValue { VariableDefinition = vdfMode, Value = "BATCH", Description = "Batch billing", Information = "" },
            new ValidValue { VariableDefinition = vdfDebug, Value = "Y", Description = "On", Information = "" },
            new ValidValue { VariableDefinition = vdfDebug, Value = "N", Description = "Off", Information = "" });

        db.VariableValues.AddRange(
            new VariableValue { VariableDefinition = vdfRetries, Scope = scpDefault, Value = "3" },
            new VariableValue { VariableDefinition = vdfRetries, Scope = scpProd, Value = "5" },
            new VariableValue { VariableDefinition = vdfMode, Scope = scpProd, Value = "REALTIME" },
            new VariableValue { VariableDefinition = vdfMode, Scope = scpDev, Value = "BATCH" },
            new VariableValue { VariableDefinition = vdfDebug, Scope = scpDev, Value = "Y" },
            new VariableValue { VariableDefinition = vdfDebug, Scope = scpDefault, Value = "N" });

        // Seed GoAnywhere variables from XML
        SeedGoAnywhereVariables(db, xtnGoAnywhere, tString, tInt, tBool, scpDefault);

        db.SaveChanges();
    }

    /// <summary>
    /// Seeds GoAnywhere project variables from the embedded XML data file.
    /// </summary>
    private static void SeedGoAnywhereVariables(
        ConfigDbContext db,
        Extent extent,
        VariableType stringType,
        VariableType intType,
        VariableType boolType,
        Scope defaultScope)
    {
        try
        {
            var xmlContent = ReadEmbeddedResource("GoAnywhereProjects.xml");
            if (string.IsNullOrWhiteSpace(xmlContent))
                return;

            var parsedVariables = GoAnywhereXmlParser.ParseVariables(xmlContent);

            var validValuesToAdd = new List<ValidValue>();
            var variableValuesToAdd = new List<VariableValue>();
            var definitionsToAdd = new List<VariableDefinition>();

            foreach (var pvar in parsedVariables)
            {
                var varType = pvar.Type switch
                {
                    "BOOLEAN" => boolType,
                    "INTEGER" => intType,
                    _ => stringType
                };

                var vdf = new VariableDefinition
                {
                    Extent = extent,
                    VariableType = varType,
                    Name = pvar.Name,
                    Description = pvar.Description,
                    ValuesAreRestricted = pvar.Type == "BOOLEAN",
                    Information = $"GoAnywhere project variable"
                };

                definitionsToAdd.Add(vdf);

                // Add valid values for BOOLEAN types
                if (pvar.Type == "BOOLEAN")
                {
                    validValuesToAdd.Add(new ValidValue { VariableDefinition = vdf, Value = "Y", Description = "Yes/True", Information = "" });
                    validValuesToAdd.Add(new ValidValue { VariableDefinition = vdf, Value = "N", Description = "No/False", Information = "" });
                }

                // Add the variable value
                variableValuesToAdd.Add(new VariableValue
                {
                    VariableDefinition = vdf,
                    Scope = defaultScope,
                    Value = pvar.Value
                });
            }

            db.VariableDefinitions.AddRange(definitionsToAdd);
            db.SaveChanges();

            db.ValidValues.AddRange(validValuesToAdd);
            db.SaveChanges();

            db.VariableValues.AddRange(variableValuesToAdd);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error seeding GoAnywhere variables: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads an embedded resource file from the Data folder.
    /// </summary>
    private static string ReadEmbeddedResource(string fileName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"ConfigSystem.Api.Data.{fileName}";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return "";

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
        catch
        {
            return "";
        }
    }
}
