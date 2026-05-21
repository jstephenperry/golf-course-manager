using System.Net.Http.Json;
using FairwayHq.Api.Endpoints;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests;

public class ImportTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ImportTests(ApiFactory factory) => _factory = factory;

    // Use globally-unique ids so this suite is safe alongside the shared
    // ApiFactory (other test classes may seed data of their own).
    private static string UniqueId(string prefix) =>
        $"{prefix}_test_{Guid.NewGuid():N}".Substring(0, prefix.Length + 6 + 12);

    [Fact]
    public async Task Import_members_happy_path_creates_all_valid_rows()
    {
        var client = _factory.CreateClient();
        var id1 = UniqueId("mbr");
        var id2 = UniqueId("mbr");
        var rows = new List<MemberDto>
        {
            NewMember(id1, "Casey", "Lin"),
            NewMember(id2, "Avery", "Stone"),
        };

        var res = await client.PostAsJsonAsync("/api/import/members", rows);
        res.EnsureSuccessStatusCode();
        var result = await res.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(2, result!.Created);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);

        var listed = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.Contains(listed!, m => m.Id == id1);
        Assert.Contains(listed!, m => m.Id == id2);
    }

    [Fact]
    public async Task Import_skips_existing_ids_and_lands_remaining_valid_rows()
    {
        var client = _factory.CreateClient();
        var alreadyId = UniqueId("mbr");
        // Pre-seed one row.
        await client.PostAsJsonAsync("/api/import/members", new[] { NewMember(alreadyId, "First", "Run") });

        // Second batch: the pre-seeded id should skip; the new one should land.
        var newId = UniqueId("mbr");
        var rows = new List<MemberDto>
        {
            NewMember(alreadyId, "Second", "Run"),
            NewMember(newId, "Newly", "Imported"),
        };

        var res = await client.PostAsJsonAsync("/api/import/members", rows);
        var result = await res.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(1, result!.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Equal("id_exists", result.Errors[0].Error);
        Assert.Equal(alreadyId, result.Errors[0].Id);
    }

    [Fact]
    public async Task Import_records_validation_errors_but_commits_valid_rows()
    {
        var client = _factory.CreateClient();
        var validId = UniqueId("mbr");
        var rows = new List<MemberDto>
        {
            // Index 0: invalid (no FirstName)
            NewMember(UniqueId("mbr"), "", "Doe"),
            // Index 1: valid
            NewMember(validId, "Valid", "Member"),
        };

        var res = await client.PostAsJsonAsync("/api/import/members", rows);
        var result = await res.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(1, result!.Created);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.Errors[0].Index);
        Assert.Equal("required_field_missing", result.Errors[0].Error);

        var listed = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.Contains(listed!, m => m.Id == validId);
    }

    [Fact]
    public async Task Import_tee_times_rejects_unknown_course_fk()
    {
        var client = _factory.CreateClient();
        // Provision one valid course.
        var courseId = UniqueId("crs");
        await client.PostAsJsonAsync("/api/import/courses", new[]
        {
            new CourseDto(courseId, "Imported Course", null, null, 70.0, 130, "Open", "06:00", "18:00", ""),
        });

        var teeId = UniqueId("tee");
        var bogusCourseId = "crs_does_not_exist";
        var rows = new List<TeeTimeDto>
        {
            new(teeId, "2026-06-01", "08:00", bogusCourseId, new List<string>(), false, "Booked", ""),
        };

        var res = await client.PostAsJsonAsync("/api/import/tee-times", rows);
        var result = await res.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(0, result!.Created);
        Assert.Single(result.Errors);
        Assert.Equal("fk_missing", result.Errors[0].Error);
    }

    [Fact]
    public async Task Import_tee_times_resolves_FK_against_existing_data_only()
    {
        // Confirms the "FK must already exist" decision: a TeeTime
        // referencing a course included earlier in the SAME request goes
        // through only because the previous course Import endpoint
        // landed the row before this call.
        var client = _factory.CreateClient();
        var courseId = UniqueId("crs");
        var courseRes = await client.PostAsJsonAsync("/api/import/courses", new[]
        {
            new CourseDto(courseId, "Imported Course", null, null, 35.0, 120, "Open", "07:00", "17:00", ""),
        });
        var cr = await courseRes.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(1, cr!.Created);

        var teeId = UniqueId("tee");
        var teeRes = await client.PostAsJsonAsync("/api/import/tee-times", new[]
        {
            new TeeTimeDto(teeId, "2026-06-01", "08:00", courseId, new List<string>(), false, "Booked", ""),
        });
        var tr = await teeRes.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(1, tr!.Created);
        Assert.Empty(tr.Errors);
    }

    [Fact]
    public async Task Import_products_happy_path()
    {
        var client = _factory.CreateClient();
        var id = UniqueId("prod");
        var rows = new List<ProductDto>
        {
            new(id, "Imported Glove", "Accessories", "IMP-GLV-1", 24.99m, 9m, 30, 6),
        };
        var res = await client.PostAsJsonAsync("/api/import/products", rows);
        var result = await res.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(1, result!.Created);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Import_tee_times_is_idempotent_via_natural_key_dedup()
    {
        // The user's pain: re-running the same payload (with empty ids)
        // used to create duplicates because the server auto-generated a
        // fresh id each time. Natural-key dedup on (date, time, courseId)
        // makes the re-run a no-op.
        var client = _factory.CreateClient();
        var courseId = UniqueId("crs");
        await client.PostAsJsonAsync("/api/import/courses", new[]
        {
            new CourseDto(courseId, "Idempotent Course", null, null, 0, 0, "Open", "06:00", "20:00", ""),
        });

        var payload = new[]
        {
            new TeeTimeDto("", "2026-06-01", "08:00", courseId, new List<string>(), false, "Booked", ""),
            new TeeTimeDto("", "2026-06-01", "08:15", courseId, new List<string>(), false, "Booked", ""),
        };

        var first = await client.PostAsJsonAsync("/api/import/tee-times", payload);
        var firstResult = await first.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(2, firstResult!.Created);
        Assert.Equal(0, firstResult.Skipped);

        // Re-run with the same payload.
        var second = await client.PostAsJsonAsync("/api/import/tee-times", payload);
        var secondResult = await second.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(0, secondResult!.Created);
        Assert.Equal(2, secondResult.Skipped);
        Assert.Equal(2, secondResult.Errors.Count);
        Assert.All(secondResult.Errors, e =>
            Assert.Equal("duplicate_natural_key", e.Error));
    }

    [Fact]
    public async Task Import_members_dedups_by_email_case_insensitive()
    {
        var client = _factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/import/members", new[]
        {
            NewMember(UniqueId("mbr"), "Casey", "Lin") with { Email = $"DuplicateEmail_{Guid.NewGuid():N}@x.example" },
        });
        var firstResult = await first.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        var createdId = firstResult!.Errors.Count == 0
            ? null  // first import succeeded; we need to grab the email for the second
            : firstResult.Errors[0].Detail;
        Assert.Equal(1, firstResult.Created);

        // Pull the email we just used (case-randomized for the second try).
        var members = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        var seeded = members!.First(m => m.FirstName == "Casey");
        var upperedEmail = seeded.Email.ToUpperInvariant();

        var second = await client.PostAsJsonAsync("/api/import/members", new[]
        {
            NewMember(UniqueId("mbr"), "Different", "Person") with { Email = upperedEmail },
        });
        var secondResult = await second.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(0, secondResult!.Created);
        Assert.Single(secondResult.Errors);
        Assert.Equal("duplicate_natural_key", secondResult.Errors[0].Error);
        Assert.Contains(seeded.Id, secondResult.Errors[0].Detail!);
        // unused — silence the analyzer if any
        _ = createdId;
    }

    [Fact]
    public async Task Import_dedups_within_a_single_batch()
    {
        // Same SKU twice in one upload — second row should skip rather
        // than create a phantom duplicate.
        var client = _factory.CreateClient();
        var sku = $"SKU-{Guid.NewGuid():N}".Substring(0, 18);
        var res = await client.PostAsJsonAsync("/api/import/products", new[]
        {
            new ProductDto("", "Wedge A", "Clubs", sku, 199m, 110m, 5, 2),
            new ProductDto("", "Wedge A (dup)", "Clubs", sku, 199m, 110m, 5, 2),
        });
        var result = await res.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(1, result!.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Equal("duplicate_natural_key", result.Errors[0].Error);
        Assert.Equal(1, result.Errors[0].Index);
    }

    [Fact]
    public async Task Import_maintenance_accepts_rows_with_omitted_optional_fks()
    {
        // Regression: the import schema documents courseId and assignedTo
        // as optional on maintenance, but the underlying TEXT columns are
        // NOT NULL. Omitting the fields used to land a null at SaveChanges
        // and surface as a 500. The endpoint coerces null → "" before
        // Apply, so rows without those fields should create cleanly.
        var client = _factory.CreateClient();
        var id = UniqueId("mnt");
        // Build the JSON payload directly so courseId / assignedTo are
        // truly omitted (not present-but-null) — the failure mode the
        // seed dataset hit.
        var payload = $"[{{\"id\":\"{id}\",\"title\":\"Re-sod tee boxes\",\"category\":\"Tees\",\"dueDate\":\"2026-05-25\",\"priority\":\"High\",\"status\":\"Open\",\"notes\":\"\"}}]";
        var res = await client.PostAsync(
            "/api/import/maintenance",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var result = await res.Content.ReadFromJsonAsync<ImportEndpoints.ImportResult>();
        Assert.Equal(1, result!.Created);
        Assert.Empty(result.Errors);
    }

    private static MemberDto NewMember(string id, string first, string last) =>
        new(Id: id, FirstName: first, LastName: last,
            Email: $"{(string.IsNullOrEmpty(first) ? "blank" : first)}@example.com",
            Phone: "555", Tier: "Full",
            Handicap: 0, JoinDate: "2024-01-01",
            Active: true, Balance: 0m, Status: "Active",
            OldestUnpaidChargeAt: null, SuspendedAt: null, Notes: null);
}
