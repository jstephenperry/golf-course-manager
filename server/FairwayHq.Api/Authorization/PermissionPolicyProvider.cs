using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FairwayHq.Api.Authorization;

/// <summary>
/// Builds <see cref="AuthorizationPolicy"/> instances on demand for any
/// policy name of the form <c>perm:&lt;permission&gt;</c> — e.g.,
/// <c>perm:tee-times:read</c>. This means endpoints register policies by
/// permission name without us hand-listing all 40+ at startup.
///
/// Falls back to the framework default policy provider for everything
/// else (e.g., the implicit default + fallback policies).
/// </summary>
public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public const string Prefix = "perm:";

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var permission = policyName.Substring(Prefix.Length);
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(PermissionClaimsTransformation.PermissionClaimType, permission)
                .Build();
        }
        return await base.GetPolicyAsync(policyName);
    }
}

/// <summary>
/// Sugar so endpoints can read as
/// <c>.RequireAuthorization(Policy.For(Permissions.MembersRead))</c>.
/// </summary>
public static class Policy
{
    public static string For(string permission)
        => PermissionPolicyProvider.Prefix + permission;
}
