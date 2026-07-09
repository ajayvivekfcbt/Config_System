using ConfigSystem.Api.Data;
using ConfigSystem.Api.Models;
using ConfigSystem.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

// The active data source (Dev or FCB) is chosen per request via the
// X-Config-Source header; each source maps to its own SQLite database.
builder.Services.AddDbContext<ConfigDbContext>((sp, o) =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>();
    var config = sp.GetRequiredService<IConfiguration>();
    var source = ConfigSource.Resolve(http.HttpContext);
    o.UseSqlite(ConfigSource.ConnectionString(config, source));
});
builder.Services.AddScoped<ConfigResolutionService>();
builder.Services.AddSingleton<As400AuthService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Create and seed a database for every source (Dev and FCB) so switching
// sources always lands on a ready-to-use store.
foreach (var source in ConfigSource.Known)
{
    var connectionString = ConfigSource.ConnectionString(app.Configuration, source);
    var options = new DbContextOptionsBuilder<ConfigDbContext>().UseSqlite(connectionString).Options;
    using var db = new ConfigDbContext(options);
    db.Database.EnsureCreated();
    // Prefer the real data exported from IBM i; fall back to the demo seed.
    if (!DataImporter.ImportAll(db))
        SeedData.EnsureSeeded(db);

    // The exported data only has DEV-server scopes/values. For the FCB source,
    // clone them onto the FCB server so it resolves values of its own.
    if (source == "Fcb")
        FcbSeeder.EnsureFcbServerScopes(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

// Writes are rejected when the read-only (FCB) source is selected.
static IResult ReadOnlyResult() =>
    Results.Json(new { error = "The FCB source is read-only. Switch to the Dev source to make changes." },
        statusCode: StatusCodes.Status403Forbidden);

// ---- Generic CRUD mapper (replaces the repeating UT20x0 work-with programs) ----
static void MapCrud<T>(WebApplication app, string route, Func<ConfigDbContext, DbSet<T>> set,
    Action<T, T> copyInto) where T : class
{
    var grp = app.MapGroup($"/api/{route}").WithTags(route);

    grp.MapGet("/", async (ConfigDbContext db) => await set(db).AsNoTracking().ToListAsync());

    grp.MapGet("/{id:int}", async (int id, ConfigDbContext db) =>
        await set(db).FindAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

    grp.MapPost("/", async (T input, HttpContext http, ConfigDbContext db) =>
    {
        if (ConfigSource.IsReadOnly(http)) return ReadOnlyResult();
        set(db).Add(input);
        await db.SaveChangesAsync();
        var id = db.Entry(input).Property("Id").CurrentValue;
        return Results.Created($"/api/{route}/{id}", input);
    });

    grp.MapPut("/{id:int}", async (int id, T input, HttpContext http, ConfigDbContext db) =>
    {
        if (ConfigSource.IsReadOnly(http)) return ReadOnlyResult();
        var existing = await set(db).FindAsync(id);
        if (existing is null) return Results.NotFound();
        copyInto(input, existing);
        await db.SaveChangesAsync();
        return Results.Ok(existing);
    });

    grp.MapDelete("/{id:int}", async (int id, HttpContext http, ConfigDbContext db) =>
    {
        if (ConfigSource.IsReadOnly(http)) return ReadOnlyResult();
        var existing = await set(db).FindAsync(id);
        if (existing is null) return Results.NotFound();
        set(db).Remove(existing);
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
}

MapCrud<VariableType>(app, "variable-types", db => db.VariableTypes, (s, d) =>
    { d.Name = s.Name; d.Description = s.Description; d.ValidationProcedure = s.ValidationProcedure; d.Information = s.Information; });

MapCrud<ScopeResolutionMethod>(app, "resolution-methods", db => db.ScopeResolutionMethods, (s, d) =>
    { d.Name = s.Name; d.Description = s.Description; d.Information = s.Information; });

MapCrud<Server>(app, "servers", db => db.Servers, (s, d) =>
    { d.Name = s.Name; d.Description = s.Description; d.MatchValue = s.MatchValue; d.Information = s.Information; });

MapCrud<Context>(app, "contexts", db => db.Contexts, (s, d) =>
    { d.Name = s.Name; d.Description = s.Description; d.IsServer = s.IsServer; d.Information = s.Information; });

MapCrud<Extent>(app, "extents", db => db.Extents, (s, d) =>
    { d.ContextId = s.ContextId; d.Name = s.Name; d.Description = s.Description; d.Information = s.Information; });

MapCrud<Scope>(app, "scopes", db => db.Scopes, (s, d) =>
    { d.ServerId = s.ServerId; d.ScopeResolutionMethodId = s.ScopeResolutionMethodId; d.Name = s.Name; d.Description = s.Description; d.IsServer = s.IsServer; d.MatchValue = s.MatchValue; d.Information = s.Information; });

MapCrud<VariableDefinition>(app, "variable-definitions", db => db.VariableDefinitions, (s, d) =>
    { d.ExtentId = s.ExtentId; d.VariableTypeId = s.VariableTypeId; d.Name = s.Name; d.Description = s.Description; d.ValuesAreRestricted = s.ValuesAreRestricted; d.Information = s.Information; });

MapCrud<ValidValue>(app, "valid-values", db => db.ValidValues, (s, d) =>
    { d.VariableDefinitionId = s.VariableDefinitionId; d.Value = s.Value; d.Description = s.Description; d.Information = s.Information; });

MapCrud<VariableValue>(app, "variable-values", db => db.VariableValues, (s, d) =>
    { d.VariableDefinitionId = s.VariableDefinitionId; d.ScopeId = s.ScopeId; d.Value = s.Value; });

// ---- Consumption / resolution endpoint (replaces UT2061-UT2087) ----
app.MapGet("/api/resolve", async (string variable, string? server, string? scope, ConfigResolutionService svc) =>
        Results.Ok(await svc.ResolveAsync(variable, server, scope)))
    .WithTags("resolution");

// ---- Same resolution logic but accepting a JSON body instead of query-string params ----
app.MapPost("/api/resolve", async (ResolveRequest req, ConfigResolutionService svc) =>
    {
        if (string.IsNullOrWhiteSpace(req.Variable))
            return Results.BadRequest(new { error = "The 'variable' field is required." });
        return Results.Ok(await svc.ResolveAsync(req.Variable, req.Server, req.Scope));
    })
    .WithTags("resolution");

// ---- All candidate values (one per scope) when no explicit scope override is chosen ----
app.MapGet("/api/resolve-all", async (string variable, string? server, ConfigResolutionService svc) =>
        Results.Ok(await svc.ResolveAllAsync(variable, server)))
    .WithTags("resolution");

// ---- AS/400 (IBM i) sign-in: validate the supplied user profile/password ----
app.MapPost("/api/login", (LoginRequest req, As400AuthService auth) =>
{
    var (ok, error) = auth.Validate(req.UserId, req.Password);
    return ok
        ? Results.Ok(new { userId = (req.UserId ?? string.Empty).Trim().ToUpperInvariant() })
        : Results.Json(new { error = error ?? "Invalid credentials." }, statusCode: StatusCodes.Status401Unauthorized);
}).WithTags("auth");

app.Run();

record LoginRequest(string UserId, string Password);
record ResolveRequest(string Variable, string? Server, string? Scope);
