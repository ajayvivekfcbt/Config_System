using ConfigSystem.Api.Data;
using ConfigSystem.Api.Models;
using ConfigSystem.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddControllers();

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

// Create and seed a database for every source (Dev and FCB)
try
{
    Console.WriteLine("[STARTUP] Initializing databases...");
    
    foreach (var source in ConfigSource.Known)
    {
        try
        {
            Console.WriteLine($"[STARTUP] Processing source: {source}");
            var connectionString = ConfigSource.ConnectionString(app.Configuration, source);
            var options = new DbContextOptionsBuilder<ConfigDbContext>().UseSqlite(connectionString).Options;
            using var db = new ConfigDbContext(options);
            db.Database.EnsureCreated();
            // Prefer the real data exported from IBM i; fall back to the demo seed.
            if (!DataImporter.ImportAll(db))
                SeedData.EnsureSeeded(db);

            // For the FCB source, seed the local DATCOMN production clone unless the FCB
            // data has already been staged live from the AS/400. Staging is on-demand
            // (see POST /api/fcb/refresh), not run on every startup.
            if (source == "Fcb" && !Fcb400Stager.IsStaged(app.Configuration))
                FcbSeeder.EnsureFcbServerScopes(db);
            
            // Seed GoAnywhere configuration data (CSV projects + XML configurations)
            var basePath = app.Environment.ContentRootPath; // Use application's content root
            var seeder = new GoAnywhereSeeder(db, basePath);
            await seeder.SeedAsync();
            
            Console.WriteLine($"[STARTUP] ✓ Source {source} initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STARTUP] ✗ Error initializing source {source}: {ex.Message}");
            Console.WriteLine($"[STARTUP] Stack: {ex.StackTrace}");
            // Don't exit, continue with other sources
        }
    }
    
    Console.WriteLine("[STARTUP] Database initialization complete");
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP] FATAL: {ex.Message}");
    Console.WriteLine($"[STARTUP] {ex.StackTrace}");
    throw;
}

Console.WriteLine("[STARTUP] Configuring middleware...");
app.UseSwagger();
app.UseSwaggerUI();
app.UseSession();
app.UseCors();
app.MapControllers();

Console.WriteLine("[STARTUP] ✓ Application ready to receive requests");

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
app.MapGet("/api/resolve", async (string variable, string? server, string? scope, int? variableId, ConfigResolutionService svc) =>
        Results.Ok(await svc.ResolveAsync(variable, server, scope, variableId)))
    .WithTags("resolution");

// ---- Same resolution logic but accepting a JSON body instead of query-string params ----
app.MapPost("/api/resolve", async (ResolveRequest req, ConfigResolutionService svc) =>
    {
        if (string.IsNullOrWhiteSpace(req.Variable))
            return Results.BadRequest(new { error = "The 'variable' field is required." });
        return Results.Ok(await svc.ResolveAsync(req.Variable, req.Server, req.Scope, req.VariableId));
    })
    .WithTags("resolution");

// ---- All candidate values (one per scope) when no explicit scope override is chosen ----
app.MapGet("/api/resolve-all", async (string variable, string? server, int? variableId, ConfigResolutionService svc) =>
        Results.Ok(await svc.ResolveAllAsync(variable, server, variableId)))
    .WithTags("resolution");

// ---- AS/400 (IBM i) sign-in: validate the supplied user profile/password ----
app.MapPost("/api/login", (LoginRequest req, HttpContext http, As400AuthService auth) =>
{
    var (ok, error) = auth.Validate(req.UserId, req.Password);
    if (ok)
    {
        // Store credentials in session for IFS service to use
        http.Session.SetString("uid", req.UserId);
        http.Session.SetString("pwd", req.Password);
        return Results.Ok(new { userId = (req.UserId ?? string.Empty).Trim().ToUpperInvariant() });
    }
    return Results.Json(new { error = error ?? "Invalid credentials." }, statusCode: StatusCodes.Status401Unauthorized);
}).WithTags("auth");

// ---- On-demand staging: refresh the FCB source live from the FCB AS/400 ----
app.MapPost("/api/fcb/refresh", (FcbRefreshRequest? req, HttpContext http, ConfigDbContext db, IConfiguration config, ILoggerFactory lf) =>
{
    if (ConfigSource.Resolve(http) != "Fcb")
        return Results.BadRequest(new { error = "Switch to the FCB source to refresh from the AS/400." });
    if (Fcb400Stager.TryStage(db, config, lf.CreateLogger("Fcb400Stager"), req?.UserId, req?.Password))
    {
        Fcb400Stager.MarkStaged(config);
        return Results.Ok(new { staged = true });
    }
    return Results.Json(new { staged = false, error = "FCB AS/400 staging is not configured or failed." },
        statusCode: StatusCodes.Status502BadGateway);
}).WithTags("fcb");

// Final startup message
Console.WriteLine("");
Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Config System API - Starting HTTP Server              ║");
Console.WriteLine("║  Listening on: http://localhost:5198                  ║");
Console.WriteLine("║  Swagger UI: http://localhost:5198/swagger            ║");
Console.WriteLine("║  Ready to accept requests                             ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝");
Console.WriteLine("");

app.Run();

record LoginRequest(string UserId, string Password);
record ResolveRequest(string Variable, string? Server, string? Scope, int? VariableId = null);
record FcbRefreshRequest(string? UserId, string? Password);
