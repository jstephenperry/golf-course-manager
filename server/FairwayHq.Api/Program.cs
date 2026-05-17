using System.Text.Json;
using System.Text.Json.Serialization;
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

// ---------- Routes ----------
app.MapAll();
app.MapTabs();
app.MapMembership();
app.MapOps();

// SPA fallback for non-/api paths: serve index.html
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
});

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
