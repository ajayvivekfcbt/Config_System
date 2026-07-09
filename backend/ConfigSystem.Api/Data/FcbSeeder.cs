using ConfigSystem.Api.Models;

namespace ConfigSystem.Api.Data;

/// <summary>
/// Makes the FCB data source resolve values of its own. The exported dataset
/// only carries scopes/values for the DEV server, so resolving against the FCB
/// server returns nothing. For the FCB database we clone every non-FCB scope
/// (and its variable values) onto the FCB server, mirroring the legacy staging
/// model where config is staged between the DEV and FCB systems. This is only
/// applied to the FCB source, so the Dev and FCB sources genuinely differ.
/// </summary>
public static class FcbSeeder
{
    public static void EnsureFcbServerScopes(ConfigDbContext db)
    {
        var fcb = db.Servers.FirstOrDefault(s => s.Name == "FCB");
        if (fcb is null) return;

        // Idempotent: if the FCB server already has scopes, there is nothing to do.
        if (db.Scopes.Any(s => s.ServerId == fcb.Id)) return;

        // Clone every scope that isn't already on the FCB server (i.e. the DEV scopes).
        var sourceScopes = db.Scopes.Where(s => s.ServerId != fcb.Id).ToList();
        if (sourceScopes.Count == 0) return;

        var scopeMap = new Dictionary<int, Scope>();
        foreach (var s in sourceScopes)
        {
            var clone = new Scope
            {
                ServerId = fcb.Id,
                ScopeResolutionMethodId = s.ScopeResolutionMethodId,
                Name = s.Name,
                Description = s.Description,
                IsServer = s.IsServer,
                MatchValue = s.MatchValue,
                Information = s.Information,
            };
            db.Scopes.Add(clone);
            scopeMap[s.Id] = clone;
        }
        db.SaveChanges(); // assign ids to the new FCB scopes

        // Copy each variable value from a DEV scope onto its cloned FCB scope.
        var sourceIds = scopeMap.Keys.ToHashSet();
        var values = db.VariableValues.Where(v => sourceIds.Contains(v.ScopeId)).ToList();
        foreach (var v in values)
        {
            db.VariableValues.Add(new VariableValue
            {
                VariableDefinitionId = v.VariableDefinitionId,
                ScopeId = scopeMap[v.ScopeId].Id,
                Value = v.Value,
            });
        }
        db.SaveChanges();
    }
}
