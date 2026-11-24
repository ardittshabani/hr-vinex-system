using Microsoft.AspNetCore.Authorization;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Infrastructure.Security;

namespace Vinex.HR.API.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Grants a permission when the caller's role includes it. Callers who must change their password
/// are denied everything except endpoints that only require authentication (change-password, me, logout).
/// </summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.FindFirst(AppClaims.MustChangePassword)?.Value == "true")
        {
            return Task.CompletedTask;
        }

        var role = context.User.FindFirst(AppClaims.Role)?.Value;
        if (RolePermissions.Has(role, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
