using System.Data.Common;
using FairwayHq.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayHq.Api.Tests;

/// <summary>
/// WebApplicationFactory variant that swaps SQLite to a per-factory in-memory
/// connection, ensuring tests are isolated from each other and from disk.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private DbConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Remove the default AppDbContext registration
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlite(_connection, sql =>
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        });
    }

    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}

public static class HttpClientJsonExtensions
{
    public static async Task<T> GetJsonAsync<T>(
        this HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<T>(json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            })!;
    }
}
