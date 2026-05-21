using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace FairwayHq.Api.Authorization;

/// <summary>
/// Runs after every successful authentication. Picks the user's realm
/// roles out of whatever shape the token (or test handler) put them
/// in, looks them up in <see cref="RolePermissions"/>, and writes one
/// <c>permission</c> claim per granted permission so endpoints can do
/// a flat <c>RequireClaim("permission", "...")</c> check.
///
/// The transformation runs on every request; the resulting principal
/// is short-lived (request-scoped) so there's no caching concern.
/// </summary>
public class PermissionClaimsTransformation : IClaimsTransformation
{
    public const string PermissionClaimType = "permission";
    public const string RoleClaimType = ClaimTypes.Role;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is null || !principal.Identity.IsAuthenticated)
            return Task.FromResult(principal);

        // Already transformed? Bail — IClaimsTransformation runs on every
        // request and we don't want to double-stamp.
        if (principal.HasClaim(c => c.Type == PermissionClaimType))
            return Task.FromResult(principal);

        var roles = ExtractRoles(principal).ToList();
        if (roles.Count == 0) return Task.FromResult(principal);

        var permissions = RolePermissions.PermissionsFor(roles);
        if (permissions.Count == 0) return Task.FromResult(principal);

        // Build a new identity copy with the additional permission claims.
        var identity = (ClaimsIdentity)principal.Identity;
        var enriched = identity.Clone();
        foreach (var perm in permissions)
        {
            enriched.AddClaim(new Claim(PermissionClaimType, perm));
        }
        // Also make sure each role is materialized as a standard role
        // claim so [Authorize(Roles=...)] still works for callers that
        // prefer the coarse shape.
        foreach (var role in roles)
        {
            if (!principal.IsInRole(role))
                enriched.AddClaim(new Claim(RoleClaimType, role));
        }
        return Task.FromResult(new ClaimsPrincipal(enriched));
    }

    /// <summary>
    /// Roles can arrive in three different shapes depending on issuer:
    ///   1. Standard <c>ClaimTypes.Role</c> claims (test handler emits these)
    ///   2. A JSON string in the <c>realm_access</c> claim (Keycloak default)
    ///   3. A bare <c>roles</c> claim (some IdPs / mapped tokens)
    /// We accept all three so the matrix lookup is shape-agnostic.
    /// </summary>
    private static IEnumerable<string> ExtractRoles(ClaimsPrincipal principal)
    {
        // Shape 1: standard role claims.
        foreach (var c in principal.FindAll(RoleClaimType))
        {
            yield return c.Value;
        }

        // Shape 2: Keycloak's realm_access.roles (JSON-encoded).
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrEmpty(realmAccess))
        {
            string[]? parsed = null;
            try
            {
                using var doc = JsonDocument.Parse(realmAccess);
                if (doc.RootElement.TryGetProperty("roles", out var rolesEl)
                    && rolesEl.ValueKind == JsonValueKind.Array)
                {
                    parsed = rolesEl.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // Malformed claim — ignore. Defensive only; should not
                // happen with a Keycloak-issued token.
            }
            if (parsed is not null)
            {
                foreach (var r in parsed) yield return r;
            }
        }

        // Shape 3: bare "roles" claim (sometimes mapped explicitly).
        foreach (var c in principal.FindAll("roles"))
        {
            yield return c.Value;
        }
    }
}
