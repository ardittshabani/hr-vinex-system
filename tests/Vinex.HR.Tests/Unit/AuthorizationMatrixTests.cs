using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Vinex.HR.Contracts.Security;

namespace Vinex.HR.Tests.Unit;

/// <summary>
/// Fails the build when an endpoint is added without an explicit authorization decision,
/// or references a policy that does not exist.
/// </summary>
public class AuthorizationMatrixTests
{
    private static IEnumerable<Type> Controllers() =>
        typeof(Program).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    [Fact]
    public void Every_action_declares_authorization()
    {
        foreach (var controller in Controllers())
        {
            foreach (var action in Actions(controller))
            {
                var declared =
                    action.GetCustomAttributes<AuthorizeAttribute>().Any() ||
                    action.GetCustomAttributes<AllowAnonymousAttribute>().Any() ||
                    controller.GetCustomAttributes<AuthorizeAttribute>().Any() ||
                    controller.GetCustomAttributes<AllowAnonymousAttribute>().Any();
                declared.ShouldBeTrue($"{controller.Name}.{action.Name} has no [Authorize] or [AllowAnonymous]");
            }
        }
    }

    [Fact]
    public void Only_login_and_refresh_are_anonymous()
    {
        var anonymous = Controllers()
            .SelectMany(c => Actions(c).Where(a => a.GetCustomAttributes<AllowAnonymousAttribute>().Any()
                                                    || c.GetCustomAttributes<AllowAnonymousAttribute>().Any())
                .Select(a => $"{c.Name}.{a.Name}"))
            .OrderBy(x => x)
            .ToList();
        anonymous.ShouldBe(["AuthController.Login", "AuthController.Refresh"]);
    }

    [Fact]
    public void Every_policy_in_use_is_a_known_permission()
    {
        var policies = Controllers()
            .SelectMany(c => c.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(Actions(c).SelectMany(a => a.GetCustomAttributes<AuthorizeAttribute>())))
            .Select(a => a.Policy)
            .Where(p => p is not null);

        foreach (var policy in policies)
        {
            Permissions.All.ShouldContain(policy!);
        }
    }

    [Fact]
    public void User_administration_requires_the_manage_users_permission()
    {
        foreach (var name in new[] { "UsersController", "RolesController" })
        {
            var controller = Controllers().Single(c => c.Name == name);
            controller.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(Permissions.UsersManage);
        }
    }
}
