using ConfigSystem.Api.Models;

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
        db.Contexts.AddRange(ctxApp, ctxSys);

        var xtnBilling = new Extent { Context = ctxApp, Name = "BILLING", Description = "Billing module", Information = "" };
        var xtnCore = new Extent { Context = ctxSys, Name = "CORE", Description = "Core system", Information = "" };
        db.Extents.AddRange(xtnBilling, xtnCore);

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

        db.SaveChanges();
    }
}
