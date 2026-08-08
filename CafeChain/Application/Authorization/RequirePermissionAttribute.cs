using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CafeChain.Application.Authorization;

/// <summary>
/// Declares a dynamic permission policy without duplicating policy registration
/// for every RBAC catalog entry.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public RequirePermissionAttribute(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            throw new ArgumentException("Permission code is required.", nameof(permissionCode));

        Policy = PolicyPrefix + permissionCode;
    }

    public RequirePermissionAttribute(string permissionCode, string roles)
        : this(permissionCode)
    {
        if (string.IsNullOrWhiteSpace(roles))
            throw new ArgumentException("Roles are required.", nameof(roles));
        Roles = string.Join(
            ",",
            roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal));
    }
}

public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return base.GetPolicyAsync(policyName);

        var permissionCode = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionCode))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
