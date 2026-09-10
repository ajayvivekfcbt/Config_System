using ConfigSystem.Api.Data;
using ConfigSystem.Api.Models;
using ConfigSystem.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(30);
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.IsEssential = true;
});
builder.Services.AddControllers();

// Trust the Azure App Service / reverse-proxy forwarded-IP headers from localhost only.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    // Accept forwarded IPs only from the loopback (Azure front-end sits on the same host).
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Loopback, 8));
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.IPv6Loopback, 128));
});

// The active data source (Dev or FCB) is chosen per request via the
// X-Config-Source header; each source maps to its own SQL Server database.
builder.Services.AddDbContext<ConfigDbContext>((sp, o) =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>();
    var config = sp.GetRequiredService<IConfiguration>();
    var source = ConfigSource.Resolve(http.HttpContext);
    o.UseSqlServer(ConfigSource.ConnectionString(config, source));
});
builder.Services.AddScoped<ConfigResolutionService>();
builder.Services.AddSingleton<As400AuthService>();

// Persist the Data Protection key ring so encrypted sensitive config values stay
// decryptable across app restarts. Keys are written to a stable folder that can be
// overridden (e.g. an Azure mounted share) via the DataProtection:KeysPath setting.
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "dp-keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("ConfigSystem.Api")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddSingleton<SensitiveValueProtector>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Only the web app's origins may call the API from a browser.
var allowedOrigins = builder.Configuration.GetSection("Api:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:5000" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    p.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    if (builder.Environment.IsDevelopment())
        // In development allow any localhost/loopback origin so VS Code port
        // forwarding (which remaps to a different local port) works without
        // needing to enumerate every possible forwarded-port URL.
        p.SetIsOriginAllowed(origin =>
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            return uri.Host is "localhost" or "127.0.0.1" or "::1";
        });
    else
        p.WithOrigins(allowedOrigins);
}));

// Throttle callers by client IP so the API can't be hammered directly.
var permitLimit = builder.Configuration.GetValue<int?>("Api:RateLimit:PermitLimit") ?? 100;
var windowSeconds = builder.Configuration.GetValue<int?>("Api:RateLimit:WindowSeconds") ?? 10;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
            }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please slow down and try again shortly." }, token);
    };
});

var app = builder.Build();

static string GetAuditUser(HttpContext http)
{
    var userId = http.Session.GetString("uid")?.Trim();
    if (!string.IsNullOrWhiteSpace(userId)) return userId;

    var serviceUser = http.Items.TryGetValue("ServiceAuth", out var auth) && auth is true
        ? http.Request.Headers["X-User-Id"].ToString().Trim()
        : string.Empty;
    return string.IsNullOrWhiteSpace(serviceUser) ? "unknown" : serviceUser;
}

// Ensure the configured databases exist. Data import and GoAnywhere seeding are
// opt-in so normal application starts do not rewrite or inspect migrated data.
try
{
    var importLegacyData = app.Configuration.GetValue<bool>("Database:ImportLegacyData");
    var seedOnStartup = app.Configuration.GetValue<bool>("Database:SeedOnStartup");
    foreach (var source in ConfigSource.Known)
    {
        try
        {
            var connectionString = ConfigSource.ConnectionString(app.Configuration, source);
            var options = new DbContextOptionsBuilder<ConfigDbContext>().UseSqlServer(connectionString).Options;
            using var db = new ConfigDbContext(options);
            db.Database.EnsureCreated();

            if (!importLegacyData && !seedOnStartup)
                continue;

            // Wrap all seeding in a transaction so partial failures leave the DB clean.
            using var tx = db.Database.BeginTransaction();
            try
            {
                // Legacy IBM i import/staging is opt-in. The GoAnywhere production
                // deployment uses the SQL Server data migrated before installation.
                if (importLegacyData)
                {
                    if (!DataImporter.ImportAll(db))
                        SeedData.EnsureSeeded(db);

                    if (source == "Fcb" && !Fcb400Stager.IsStaged(app.Configuration))
                        FcbSeeder.EnsureFcbServerScopes(db);
                }

                if (seedOnStartup)
                {
                    var basePath = app.Environment.ContentRootPath;
                    var seeder = new GoAnywhereSeeder(db, basePath, app.Services.GetRequiredService<SensitiveValueProtector>());
                    await seeder.SeedAsync();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STARTUP] ✗ Error initializing source {source}: {ex.Message}");
            Console.WriteLine($"[STARTUP] Stack: {ex.StackTrace}");
            // Don't exit, continue with other sources
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP] FATAL: {ex.Message}");
    Console.WriteLine($"[STARTUP] {ex.StackTrace}");
    throw;
}

// Return a generic JSON error for all unhandled exceptions — no stack traces to callers.
app.UseExceptionHandler(err => err.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again later." });
}));
app.UseForwardedHeaders();
// Only redirect to HTTPS in production; locally the app runs on plain HTTP.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseSession();
app.UseCors();
app.UseRateLimiter();

// All API routes are protected by the server-side session created after IBM i
// authentication. Do not use client-provided headers as authentication because
// they can be copied from a browser request and replayed outside the UI.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isLoginRoute = HttpMethods.IsPost(context.Request.Method)
            && path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);
        if (!isLoginRoute && string.IsNullOrEmpty(context.Session.GetString("uid")))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required." });
            return;
        }
    }
    await next();
});

app.MapControllers();

#if IBMI_CONFIGURATION
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

// ---- Variable Values with Audit Logging ----
var varValGrp = app.MapGroup("/api/variable-values").WithTags("variable-values");

varValGrp.MapGet("/", async (ConfigDbContext db) => 
    await db.VariableValues.AsNoTracking().Select(v => new { v.Id, v.VariableDefinitionId, v.ScopeId, v.Value }).ToListAsync());

varValGrp.MapGet("/{id:int}", async (int id, ConfigDbContext db) =>
{
    var v = await db.VariableValues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    return v != null ? Results.Ok(new { v.Id, v.VariableDefinitionId, v.ScopeId, v.Value }) : Results.NotFound();
});

varValGrp.MapPost("/", async (VariableValue input, HttpContext http, ConfigDbContext db) =>
{
    if (ConfigSource.IsReadOnly(http)) return ReadOnlyResult();
    
    db.VariableValues.Add(input);
    await db.SaveChangesAsync();
    
    var varDef = await db.VariableDefinitions.FindAsync(input.VariableDefinitionId);
    var scope = await db.Scopes.FindAsync(input.ScopeId);
    var extent = varDef != null ? await db.Extents.FindAsync(varDef.ExtentId) : null;
    
    var auditLog = new IBMiAuditLog
    {
        VariableDefinitionId = input.VariableDefinitionId,
        ScopeId = input.ScopeId,
        ScopeName = scope?.Name ?? "Unknown",
        VariableDefName = varDef?.Name ?? "Unknown",
        ExtentName = extent?.Name ?? "Unknown",
        Environment = scope?.Name ?? "Dev",
        OldValue = null,
        NewValue = varDef?.ValuesAreRestricted == true ? "●●●●●●●●" : input.Value,
        Action = "CREATE",
        ChangedBy = GetAuditUser(http),
        IsSensitive = varDef?.ValuesAreRestricted ?? false,
        ChangedAtUtc = DateTime.UtcNow
    };
    db.IBMiAuditLogs.Add(auditLog);
    await db.SaveChangesAsync();
    
    var id = db.Entry(input).Property("Id").CurrentValue;
    return Results.Created($"/api/variable-values/{id}", new { input.Id, input.VariableDefinitionId, input.ScopeId, input.Value });
});

varValGrp.MapPut("/{id:int}", async (int id, VariableValue input, HttpContext http, ConfigDbContext db) =>
{
    if (ConfigSource.IsReadOnly(http)) return ReadOnlyResult();
    
    var existing = await db.VariableValues.FindAsync(id);
    if (existing is null) return Results.NotFound();
    
    var oldValue = existing.Value;
    existing.VariableDefinitionId = input.VariableDefinitionId;
    existing.ScopeId = input.ScopeId;
    existing.Value = input.Value;
    
    await db.SaveChangesAsync();
    
    var varDef = await db.VariableDefinitions.FindAsync(input.VariableDefinitionId);
    var scope = await db.Scopes.FindAsync(input.ScopeId);
    var extent = varDef != null ? await db.Extents.FindAsync(varDef.ExtentId) : null;
    
    var auditLog = new IBMiAuditLog
    {
        VariableDefinitionId = input.VariableDefinitionId,
        ScopeId = input.ScopeId,
        ScopeName = scope?.Name ?? "Unknown",
        VariableDefName = varDef?.Name ?? "Unknown",
        ExtentName = extent?.Name ?? "Unknown",
        Environment = scope?.Name ?? "Dev",
        OldValue = varDef?.ValuesAreRestricted == true ? "●●●●●●●●" : oldValue,
        NewValue = varDef?.ValuesAreRestricted == true ? "●●●●●●●●" : input.Value,
        Action = "UPDATE",
        ChangedBy = GetAuditUser(http),
        IsSensitive = varDef?.ValuesAreRestricted ?? false,
        ChangedAtUtc = DateTime.UtcNow
    };
    db.IBMiAuditLogs.Add(auditLog);
    await db.SaveChangesAsync();
    
    return Results.Ok(new { existing.Id, existing.VariableDefinitionId, existing.ScopeId, existing.Value });
});

varValGrp.MapDelete("/{id:int}", async (int id, HttpContext http, ConfigDbContext db) =>
{
    if (ConfigSource.IsReadOnly(http)) return ReadOnlyResult();
    
    var existing = await db.VariableValues.FindAsync(id);
    if (existing is null) return Results.NotFound();
    
    var varDef = await db.VariableDefinitions.FindAsync(existing.VariableDefinitionId);
    var scope = await db.Scopes.FindAsync(existing.ScopeId);
    var extent = varDef != null ? await db.Extents.FindAsync(varDef.ExtentId) : null;
    
    db.VariableValues.Remove(existing);
    await db.SaveChangesAsync();
    
    var auditLog = new IBMiAuditLog
    {
        VariableDefinitionId = existing.VariableDefinitionId,
        ScopeId = existing.ScopeId,
        ScopeName = scope?.Name ?? "Unknown",
        VariableDefName = varDef?.Name ?? "Unknown",
        ExtentName = extent?.Name ?? "Unknown",
        Environment = scope?.Name ?? "Dev",
        OldValue = varDef?.ValuesAreRestricted == true ? "●●●●●●●●" : existing.Value,
        NewValue = null,
        Action = "DELETE",
        ChangedBy = GetAuditUser(http),
        IsSensitive = varDef?.ValuesAreRestricted ?? false,
        ChangedAtUtc = DateTime.UtcNow
    };
    db.IBMiAuditLogs.Add(auditLog);
    await db.SaveChangesAsync();
    
    return Results.NoContent();
});

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
        http.Session.SetString("uid", req.UserId);
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
#endif

app.Run();

#if IBMI_CONFIGURATION
record LoginRequest(string UserId, string Password);
record ResolveRequest(string Variable, string? Server, string? Scope, int? VariableId = null);
record FcbRefreshRequest(string? UserId, string? Password);
#endif
