using ConfigSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ConfigSystem.Api.Services;

public record ResolvedValue(string Variable, string Server, string? Value, string? ScopeName, string? Method, bool Found, int? ValueId = null);

/// <summary>
/// Port of the original "consumption" logic (UT2061-UT2087): resolve a variable
/// definition to a concrete value for a given server. Scopes for the server are
/// considered; an EXACT match wins over a DEFAULT scope.
/// </summary>
public class ConfigResolutionService
{
    private readonly ConfigDbContext _db;
    public ConfigResolutionService(ConfigDbContext db) => _db = db;

    public async Task<ResolvedValue> ResolveAsync(string variableName, string? serverName, string? scopeName = null, int? variableId = null)
    {
        var vdf = variableId is int vid
            ? await _db.VariableDefinitions.FirstOrDefaultAsync(v => v.Id == vid)
            : await _db.VariableDefinitions.FirstOrDefaultAsync(v => v.Name == variableName);
        if (vdf is null)
            return new ResolvedValue(variableName, serverName ?? scopeName ?? "", null, null, null, false);

        // If an explicit scope is chosen, resolve directly against that scope.
        if (!string.IsNullOrWhiteSpace(scopeName))
        {
            var direct = await _db.VariableValues
                .Include(v => v.Scope)!.ThenInclude(s => s!.Server)
                .Include(v => v.Scope)!.ThenInclude(s => s!.ScopeResolutionMethod)
                .Where(v => v.VariableDefinitionId == vdf.Id && v.Scope!.Name == scopeName)
                .FirstOrDefaultAsync();

            var label = direct?.Scope!.Server?.Name ?? serverName ?? scopeName;
            return direct is null
                ? new ResolvedValue(variableName, label, null, null, null, false)
                : new ResolvedValue(variableName, label, direct.Value, direct.Scope!.Name, direct.Scope!.ScopeResolutionMethod?.Name, true, direct.Id);
        }

        if (string.IsNullOrWhiteSpace(serverName))
            return new ResolvedValue(variableName, "", null, null, null, false);

        // Candidate values for this variable whose scope's server matches the request.
        var candidates = await _db.VariableValues
            .Include(v => v.Scope)!.ThenInclude(s => s!.Server)
            .Include(v => v.Scope)!.ThenInclude(s => s!.ScopeResolutionMethod)
            .Where(v => v.VariableDefinitionId == vdf.Id)
            .Where(v => v.Scope!.Server!.Name == serverName || v.Scope!.MatchValue == "*")
            .ToListAsync();

        // Precedence: an exact server match beats a wildcard/default scope.
        var best = candidates
            .OrderByDescending(v => v.Scope!.Server!.Name == serverName ? 1 : 0)
            .ThenByDescending(v => v.Scope!.ScopeResolutionMethod!.Name == "EXACT" ? 1 : 0)
            .FirstOrDefault();

        return best is null
            ? new ResolvedValue(variableName, serverName, null, null, null, false)
            : new ResolvedValue(variableName, serverName, best.Value, best.Scope!.Name, best.Scope!.ScopeResolutionMethod!.Name, true, best.Id);
    }

    /// <summary>
    /// Returns EVERY candidate value for a variable (one per matching scope) instead of just the
    /// winning one. Used when no explicit scope override is chosen so the UI can show each match.
    /// </summary>
    public async Task<List<ResolvedValue>> ResolveAllAsync(string variableName, string? serverName, int? variableId = null)
    {
        var vdf = variableId is int vid
            ? await _db.VariableDefinitions.FirstOrDefaultAsync(v => v.Id == vid)
            : await _db.VariableDefinitions.FirstOrDefaultAsync(v => v.Name == variableName);
        if (vdf is null)
            return new List<ResolvedValue> { new ResolvedValue(variableName, serverName ?? "", null, null, null, false) };

        var query = _db.VariableValues
            .Include(v => v.Scope)!.ThenInclude(s => s!.Server)
            .Include(v => v.Scope)!.ThenInclude(s => s!.ScopeResolutionMethod)
            .Where(v => v.VariableDefinitionId == vdf.Id);

        if (!string.IsNullOrWhiteSpace(serverName))
            query = query.Where(v => v.Scope!.Server!.Name == serverName || v.Scope!.MatchValue == "*");

        var list = await query.ToListAsync();
        if (list.Count == 0)
            return new List<ResolvedValue> { new ResolvedValue(variableName, serverName ?? "", null, null, null, false) };

        return list
            .OrderByDescending(v => v.Scope!.Server?.Name == serverName ? 1 : 0)
            .ThenByDescending(v => v.Scope!.ScopeResolutionMethod!.Name == "EXACT" ? 1 : 0)
            .Select(v => new ResolvedValue(
                variableName,
                v.Scope!.Server?.Name ?? serverName ?? "",
                v.Value,
                v.Scope!.Name,
                v.Scope!.ScopeResolutionMethod!.Name,
                true,
                v.Id))
            .ToList();
    }
}
