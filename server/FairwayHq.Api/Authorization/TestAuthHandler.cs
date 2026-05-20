using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FairwayHq.Api.Authorization;

/// <summary>
/// Authentication handler used ONLY in the Testing environment. Reads
/// two headers off each request:
///   <list>
///   <item><c>X-Test-Roles</c>: comma-separated realm role names</item>
///   <item><c>X-Test-User</c>: synthetic user identity (defaults to "test-owner")</item>
///   </list>
/// If <c>X-Test-Roles</c> is absent, defaults to the <c>owner</c> role so
/// every existing test that pre-dates auth continues to pass unchanged.
/// Tests that specifically assert RBAC behavior set the header to the
/// role(s) they want to simulate.
///
/// The cryptographic JWT pipeline (issuer validation, JWKS, signature
/// verification) is fully exercised in production via JwtBearer. We
/// deliberately don't try to simulate it in tests — the test handler
/// shortcuts directly to "an authenticated principal with these roles."
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string UserHeader = "X-Test-User";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Pull the role string (default: "owner" so legacy tests pass)
        var rolesHeader = Request.Headers.TryGetValue(RolesHeader, out var r)
            ? r.ToString()
            : Roles.Owner;
        var roles = rolesHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        var user = Request.Headers.TryGetValue(UserHeader, out var u)
            ? u.ToString()
            : "test-owner";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user),
            new(ClaimTypes.Name, user),
            new("preferred_username", user),
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
