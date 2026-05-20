using System.Text.Json;
using System.Text.Json.Serialization;
using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Endpoints;
using FairwayHq.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<DunningService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DunningService>());

// Authentication + authorization: JWT bearer against Keycloak in prod /
// dev; an in-memory test handler in the Testing env. See ADR 0003.
builder.Services.AddFairwayAuth(builder.Configuration, builder.Environment);

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
