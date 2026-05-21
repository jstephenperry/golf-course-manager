namespace FairwayHq.Api.Authorization;

/// <summary>
/// Realm-role names exactly as they appear in Keycloak. Constants so a
/// typo is a compile error.
///
/// Role assignment is Keycloak's job. The app receives whatever roles
/// the token carries and looks them up in <see cref="RolePermissions"/>.
/// </summary>
public static class Roles
{
    public const string Owner = "owner";
    public const string Manager = "manager";
    public const string Pro = "pro";
    public const string AssistantPro = "assistant-pro";
    public const string ProShop = "pro-shop";
    public const string Greenkeeper = "greenkeeper";
    public const string Starter = "starter";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Owner, Manager, Pro, AssistantPro, ProShop, Greenkeeper, Starter,
    };
}
