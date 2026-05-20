using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace FairwayHq.Api.Authorization;

/// <summary>
/// Wires authentication + authorization into the DI container. Called
/// from <c>Program.cs</c>. Splits cleanly by environment:
///   <list>
///   <item><b>Testing</b>: only the <see cref="TestAuthHandler"/> is
///   registered. Tests get a fast in-memory ClaimsPrincipal without
///   touching JWT machinery.</item>
///   <item><b>Development / Production</b>: real JWT validation
///   against the configured Keycloak realm. If no Keycloak URL is
///   configured, registers JwtBearer with a placeholder that will
///   reject all requests — fails closed.</item>
///   </list>
/// </summary>
public static class AuthSetup
{
    public static void AddFairwayAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.AddSingleton<IClaimsTransformation, PermissionClaimsTransformation>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        if (env.IsEnvironment("Testing"))
        {
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        }
        else
        {
            // Real JWT validation against Keycloak.
            //
            // Configuration keys:
            //   Authentication:Keycloak:Authority         the PUBLIC issuer URL the SPA
            //                                             redirects to and the token's
            //                                             iss claim matches.
            //   Authentication:Keycloak:MetadataAddress   (optional) internal URL the API
            //                                             uses to fetch the discovery doc
            //                                             + JWKS. Defaults to
            //                                             {Authority}/.well-known/openid-configuration.
            //                                             Use this when the public issuer
            //                                             URL is NOT reachable from the
            //                                             API host (e.g., the API is in a
            //                                             container and the issuer URL is
            //                                             the host's proxy).
            //   Authentication:Keycloak:Audience          (optional) expected aud/azp.
            //                                             Defaults to fairway-hq-spa.
            //   Authentication:Keycloak:RequireHttps      (default: true in Production)
            //
            // Separating Authority from MetadataAddress is the orthodox pattern when
            // the API runs behind a different network surface than the SPA. The token's
            // iss claim is validated against Authority; signing keys come from the
            // internal MetadataAddress. No TLS hacks needed — the API talks plain
            // HTTP to the IdP over a trusted internal network.
            // A1: Validate critical config and fail closed at startup.
            var (authority, metadataAddress, audience, requireHttps) =
                ValidateKeycloakConfig(config, env);

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;
                    options.Audience = audience;
                    options.RequireHttpsMetadata = requireHttps;
                    if (!string.IsNullOrEmpty(metadataAddress))
                    {
                        // Backchannel fetch override. Public URLs that
                        // route through a reverse proxy are typically
                        // unreachable from the API container; this lets
                        // the API hit the IdP directly on the internal
                        // network while still validating the token's
                        // public-issuer iss claim.
                        options.MetadataAddress = metadataAddress;
                    }
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        // A1: Always validate the issuer against an explicit
                        // expected value derived from Authority — never gate
                        // this on whether Authority is set. ValidateKeycloakConfig
                        // guarantees Authority is non-empty in Production.
                        ValidateIssuer = true,
                        ValidIssuer = authority,
                        // A1: Always validate the audience. Keycloak doesn't
                        // put the client id in `aud` by default — its
                        // single-client setup uses `azp` (Authorized Party).
                        // We accept tokens whose `aud` OR `azp` matches the
                        // configured audience, but an empty configured
                        // audience is a hard startup failure (see
                        // ValidateKeycloakConfig), never an accept-all.
                        ValidateAudience = true,
                        AudienceValidator = (audiences, securityToken, _) =>
                        {
                            if (audiences.Any(a => string.Equals(a, audience, StringComparison.Ordinal)))
                                return true;
                            // Fall back to azp. .NET 10's JwtBearer uses
                            // JsonWebToken; the older JwtSecurityToken
                            // type still appears in some pipelines, so
                            // we handle both.
                            string? azp = securityToken switch
                            {
                                Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jwt =>
                                    jwt.TryGetPayloadValue<string>("azp", out var v) ? v : null,
                                System.IdentityModel.Tokens.Jwt.JwtSecurityToken legacy =>
                                    legacy.Payload.TryGetValue("azp", out var lv)
                                        && lv is string ls ? ls : null,
                                _ => null,
                            };
                            return string.Equals(azp, audience, StringComparison.Ordinal);
                        },
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        // Keycloak puts the resolved role into a Role claim
                        // via realm_access — the claims transformer reads it.
                        NameClaimType = "preferred_username",
                        RoleClaimType = "roles",
                        ClockSkew = TimeSpan.FromSeconds(30),
                    };
                });
        }

        services.AddAuthorization(options =>
        {
            // DefaultPolicy: applies when an endpoint uses [Authorize]
            // without naming a specific policy. Requires a logged-in user.
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            // FallbackPolicy: applies to endpoints with NO auth metadata.
            // Default-deny — any future endpoint that forgets to opt in
            // is still locked down. Anonymous endpoints (just
            // /api/health and the SPA fallback) opt out explicitly with
            // .AllowAnonymous().
            options.FallbackPolicy = options.DefaultPolicy;
        });
    }

    /// <summary>
    /// A1: Reads + validates Keycloak auth configuration, failing closed
    /// (throwing) on a misconfiguration that would otherwise register a
    /// permissive bearer scheme. Extracted as a pure static so it can be
    /// unit-tested against an in-memory configuration without standing up
    /// a real Keycloak.
    /// </summary>
    /// <returns>(authority, metadataAddress, audience, requireHttps)</returns>
    public static (string? Authority, string? MetadataAddress, string Audience, bool RequireHttps)
        ValidateKeycloakConfig(IConfiguration config, IHostEnvironment env)
    {
        var section = config.GetSection("Authentication:Keycloak");
        var authority = section["Authority"];
        var metadataAddress = section["MetadataAddress"];
        var audience = section["Audience"] ?? "fairway-hq-spa";
        var requireHttps = section.GetValue("RequireHttps", env.IsProduction());

        // Fail closed loudly: a Production deployment with no issuer would
        // otherwise register a JwtBearer scheme that can't validate `iss`,
        // silently weakening token validation.
        if (env.IsProduction() && string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException(
                "Authentication:Keycloak:Authority must be configured in Production. " +
                "Refusing to start with an unvalidated issuer (fail closed).");
        }

        // An empty audience must be a hard failure rather than an
        // accept-all AudienceValidator.
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "Authentication:Keycloak:Audience must be a non-empty value. " +
                "Refusing to start with audience validation effectively disabled.");
        }

        return (authority, metadataAddress, audience, requireHttps);
    }
}
