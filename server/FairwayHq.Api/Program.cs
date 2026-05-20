using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Endpoints;
using FairwayHq.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// A13: cap the request body size at the server level so an oversized
// import/restore payload is rejected before it's buffered into memory.
// 32 MiB comfortably covers a full data snapshot while bounding abuse.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024 * 1024);

// ---------- Services ----------
var connection = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=fairway.db";

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(connection, sql =>
        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<DunningOptions>(
    builder.Configuration.GetSection(DunningOptions.Section));
builder.Services.Configure<MembershipOptions>(
    builder.Configuration.GetSection(MembershipOptions.Section));
builder.Services.AddSingleton<DunningService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DunningService>());

// Authentication + authorization: JWT bearer against Keycloak in prod /
// dev; an in-memory test handler in the Testing env. See ADR 0003.
builder.Services.AddFairwayAuth(builder.Configuration, builder.Environment);

// Persist DataProtection keys onto the data volume when a key-ring path is
// configured (the container image sets DataProtection:KeyRingPath to
// /app/data/keys). Without this the key ring lands in the container's
// ephemeral home dir and is regenerated on every recreate — the runtime
// logs a warning and any protected payload (antiforgery tokens, future
// cookie/session use) becomes invalid across restarts. Native `dotnet run`
// leaves this unset and keeps the framework default.
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    Directory.CreateDirectory(keyRingPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}

const string DevCors = "DevCors";
builder.Services.AddCors(o =>
{
    o.AddPolicy(DevCors, p => p
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCors);
}

// Global exception → JSON
app.UseExceptionHandler(errApp => errApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        error = "internal_error",
        traceId = context.TraceIdentifier
    });
}));

// SPA static files (only meaningful when wwwroot is populated)
app.UseDefaultFiles();
app.UseStaticFiles();

// Auth pipeline — must come after UseExceptionHandler / static files so
// public assets (the SPA shell, favicon, etc.) load without a token,
// but before route mapping so endpoints can enforce policies.
app.UseAuthentication();
app.UseAuthorization();

// ---------- Routes ----------
app.MapAll();
app.MapNines();
app.MapTabs();
app.MapMembership();
app.MapMemberOverview();
app.MapLedger();
app.MapImport();
app.MapOps();

// SPA fallback for non-/api paths: serve index.html.
// MUST stay anonymous — the SPA itself hosts the login page, so
// requesting "/" before authentication is the normal happy path.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    var index = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
    if (File.Exists(index))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(index);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("SPA bundle not present. Build the client first.");
    }
}).AllowAnonymous();

// ---------- DB init ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    Seed.EnsureSeeded(db);
}

app.Run();

// Expose for WebApplicationFactory<Program> in the test project.
public partial class Program { }
