using System.Net.Http.Json;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests.Helpers;

/// <summary>
/// Provisions the minimum fixture set the existing test suite expects,
/// using the live HTTP API. Idempotent — only POSTs entities that are
/// not already present, so it's safe to call multiple times within a
/// shared ApiFactory and safe whether or not the server-side seed is
/// running. After Phase B's Seed.cs empty-out, this becomes the sole
/// source of test data.
///
/// IDs match the documented seed values so existing test bodies that
/// hardcode "mbr_J4nKp2vQ8x" continue to work without rewrite.
/// </summary>
public static class TestSeed
{
    public const string EleanorId = "mbr_J4nKp2vQ8x";
    public const string MarcusId = "mbr_W7gHk9rTfL";
    public const string SofiaId = "mbr_P3xYm5dBqV";
    public const string DanielId = "mbr_F8jRn2KwHt";
    public const string PriyaId = "mbr_C6vDp9LqXz";

    public const string ChampionshipCourseId = "crs_K7nMpQjLxR";

    public const string ProV1ProductId = "prod_K2nM8wQjLp";
    public const string ProV1SleeveProductId = "prod_F4hLn7QxBg";
    public const string IcedTeaProductId = "prod_W6tRd3JmPk";

    public const string EleanorRound1Id = "tee_H1aLp3KqXr";
    public const string EleanorRound2Id = "tee_H2bMq8RnVw";

    public static async Task MinimalAsync(HttpClient client)
    {
        var existingMembers = await client.GetFromJsonAsync<List<MemberDto>>("/api/members")
            ?? new();
        var have = existingMembers.Select(m => m.Id).ToHashSet();

        await EnsureMember(client, have, EleanorId, "Eleanor", "Park",
            "eleanor.park@example.com", "555-0142", "Full", 8.4, "2019-03-12");
        await EnsureMember(client, have, MarcusId, "Marcus", "Reed",
            "marcus.reed@example.com", "555-0188", "Weekday", 14.2, "2021-07-01");
        await EnsureMember(client, have, SofiaId, "Sofia", "Alvarez",
            "sofia.alvarez@example.com", "555-0114", "Full", 3.1, "2015-05-22");
        await EnsureMember(client, have, DanielId, "Daniel", "O'Connell",
            "danny.oc@example.com", "555-0199", "Corporate", 22.6, "2023-01-10");
        await EnsureMember(client, have, PriyaId, "Priya", "Shah",
            "priya.shah@example.com", "555-0167", "Social", 0, "2022-09-30",
            active: false, status: "Inactive");

        var existingCourses = await client.GetFromJsonAsync<List<CourseDto>>("/api/courses")
            ?? new();
        var courseIds = existingCourses.Select(c => c.Id).ToHashSet();
        if (!courseIds.Contains(ChampionshipCourseId))
        {
            await PostOrThrow(client, "/api/courses", new CourseDto(
                Id: ChampionshipCourseId,
                Name: "Championship Course",
                Holes: 18, Par: 72, Yardage: 7124,
                Rating: 74.2, Slope: 138,
                Status: "Open",
                OpenTime: "06:00", CloseTime: "18:00",
                Notes: ""));
        }

        var existingProducts = await client.GetFromJsonAsync<List<ProductDto>>("/api/products")
            ?? new();
        var productIds = existingProducts.Select(p => p.Id).ToHashSet();
        await EnsureProduct(client, productIds, ProV1ProductId,
            "Titleist Pro V1 Dozen", "Balls", "TIT-PV1-12", 54.99m, 36m, 100, 12);
        await EnsureProduct(client, productIds, ProV1SleeveProductId,
            "Titleist Pro V1x Sleeve", "Balls", "TIT-PV1X-3", 17.99m, 9m, 100, 24);
        await EnsureProduct(client, productIds, IcedTeaProductId,
            "Iced Tea (Can)", "Food & Beverage", "FB-ICED-TEA", 3.50m, 0.80m, 100, 24);

        // Historical Completed rounds for Eleanor. Dates are computed
        // relative to today so MemberOverviewTests' "lifetime rounds"
        // assertion is stable regardless of when the suite runs.
        var existingTees = await client.GetFromJsonAsync<List<TeeTimeDto>>("/api/tee-times")
            ?? new();
        var teeIds = existingTees.Select(t => t.Id).ToHashSet();
        var today = DateTime.UtcNow.Date;
        await EnsureTeeTime(client, teeIds, EleanorRound1Id,
            today.AddDays(-7).ToString("yyyy-MM-dd"), "07:30",
            ChampionshipCourseId, new List<string> { EleanorId, SofiaId },
            cart: true, status: "Completed");
        await EnsureTeeTime(client, teeIds, EleanorRound2Id,
            today.AddDays(-14).ToString("yyyy-MM-dd"), "08:00",
            ChampionshipCourseId, new List<string> { EleanorId, MarcusId },
            cart: false, status: "Completed");
    }

    private static async Task EnsureMember(
        HttpClient client, HashSet<string> existing, string id,
        string first, string last, string email, string phone,
        string tier, double handicap, string joinDate,
        bool active = true, string? status = null)
    {
        if (existing.Contains(id)) return;
        var dto = new MemberDto(
            Id: id, FirstName: first, LastName: last,
            Email: email, Phone: phone, Tier: tier,
            Handicap: handicap, JoinDate: joinDate,
            Active: active, Balance: 0m,
            Status: status ?? (active ? "Active" : "Inactive"),
            OldestUnpaidChargeAt: null, SuspendedAt: null, Notes: null);
        await PostOrThrow(client, "/api/members", dto);
        existing.Add(id);
    }

    private static async Task EnsureProduct(
        HttpClient client, HashSet<string> existing, string id,
        string name, string category, string sku,
        decimal price, decimal cost, int stock, int reorderLevel)
    {
        if (existing.Contains(id)) return;
        var dto = new ProductDto(
            Id: id, Name: name, Category: category, Sku: sku,
            Price: price, Cost: cost, Stock: stock, ReorderLevel: reorderLevel);
        await PostOrThrow(client, "/api/products", dto);
        existing.Add(id);
    }

    private static async Task EnsureTeeTime(
        HttpClient client, HashSet<string> existing, string id,
        string date, string time, string courseId,
        List<string> players, bool cart, string status)
    {
        if (existing.Contains(id)) return;
        var dto = new TeeTimeDto(
            Id: id, Date: date, Time: time, CourseId: courseId,
            Players: players, Cart: cart, Status: status, Notes: "");
        await PostOrThrow(client, "/api/tee-times", dto);
        existing.Add(id);
    }

    private static async Task PostOrThrow<T>(HttpClient client, string path, T body)
    {
        var res = await client.PostAsJsonAsync(path, body);
        if (!res.IsSuccessStatusCode)
        {
            var msg = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"TestSeed POST {path} failed: {(int)res.StatusCode} {res.StatusCode} — {msg}");
        }
    }
}
