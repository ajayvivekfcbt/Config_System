using ConfigSystem.Api.Models;

namespace ConfigSystem.Api.Data;

/// <summary>
/// Makes the FCB data source resolve values of its own. The exported dataset
/// only carries scopes/values for the DEV server, so resolving against the FCB
/// server returns nothing. The FCB (production) system uses the DATCOMN data
/// library, so for the FCB database we clone the DEV "DATCOMN" scope (and its
/// variable values) onto the FCB server. This is only applied to the FCB
/// source, so the Dev and FCB sources genuinely differ.
/// </summary>
public static class FcbSeeder
{
    /// <summary>The production data-library scope the FCB server resolves against.</summary>
    private const string FcbScopeName = "DATCOMN";

    public static void EnsureFcbServerScopes(ConfigDbContext db)
    {
        var fcb = db.Servers.FirstOrDefault(s => s.Name == "FCB");
        if (fcb is null) return;

        // The FCB server resolves against the DATCOMN production library scope
        // and the server-level (IsServer) scope. Clone both from DEV.
        var sourceScopes = db.Scopes
            .Where(s => s.ServerId != fcb.Id && (s.Name == FcbScopeName || s.IsServer))
            .ToList();
        if (sourceScopes.Count == 0) return;

        // Already correct? FCB server has exactly the desired scopes and no others.
        var desired = sourceScopes.Select(s => s.Name).ToHashSet();
        var existing = db.Scopes.Where(s => s.ServerId == fcb.Id).ToList();
        if (existing.Count == desired.Count && existing.All(s => desired.Contains(s.Name)))
            return;

        // Otherwise reset the FCB server's scopes (and their cloned values) so a
        // previously seeded FCB database is corrected on restart.
        if (existing.Count > 0)
        {
            var oldIds = existing.Select(s => s.Id).ToHashSet();
            db.VariableValues.RemoveRange(db.VariableValues.Where(v => oldIds.Contains(v.ScopeId)));
            db.Scopes.RemoveRange(existing);
            db.SaveChanges();
        }

        // Clone the source scope(s) onto the FCB server.
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

        // Copy each variable value from the DEV scope onto its cloned FCB scope.
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
