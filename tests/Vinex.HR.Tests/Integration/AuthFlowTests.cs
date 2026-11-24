using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Vinex.HR.Contracts.Auth;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Contracts.Users;
using Vinex.HR.Infrastructure.Persistence;

namespace Vinex.HR.Tests.Integration;

[Collection("api")]
public class AuthFlowTests(ApiFixture fixture)
{
    private const string UserPassword = "Temporary-Pass-123";
    private const string NewPassword = "Brand-New-Pass-456";

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private async Task<UserDto> CreateUserAsync(string username, string role)
    {
        var admin = await fixture.GetAdminTokenAsync();
        var response = await fixture.CreateClient().SendAsync(HttpMethod.Post, "/api/v1/users", admin,
            new CreateUserRequest(username, $"Test {username}", null, role, UserPassword));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    [Fact]
    public async Task Bootstrap_admin_must_change_password_before_using_the_api()
    {
        var client = fixture.CreateClient();
        // The admin's password may already have been changed by another test; only assert on a fresh user.
        var username = Unique("fresh");
        await CreateUserAsync(username, RoleNames.HRAdministrator);

        var login = await client.LoginAsync(username, UserPassword);
        login.User.MustChangePassword.ShouldBeTrue();

        var blocked = await client.SendAsync(HttpMethod.Get, "/api/v1/users", login.AccessToken);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var changed = await client.ChangePasswordAsync(login.AccessToken, UserPassword, NewPassword);
        changed.User.MustChangePassword.ShouldBeFalse();

        var allowed = await client.SendAsync(HttpMethod.Get, "/api/v1/users", changed.AccessToken);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_with_a_generic_error()
    {
        var response = await fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("nobody-here", "whatever-password"));
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Account_locks_after_repeated_failures()
    {
        var username = Unique("lock");
        await CreateUserAsync(username, RoleNames.Management);
        var client = fixture.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "wrong-password-x"));
            bad.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Even the correct password is refused while locked.
        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, UserPassword));
        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Management_cannot_administer_users_but_can_read_their_profile()
    {
        var username = Unique("mgmt");
        await CreateUserAsync(username, RoleNames.Management);
        var client = fixture.CreateClient();

        var login = await client.LoginAsync(username, UserPassword);
        var session = await client.ChangePasswordAsync(login.AccessToken, UserPassword, NewPassword);

        (await client.SendAsync(HttpMethod.Get, "/api/v1/users", session.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.SendAsync(HttpMethod.Get, "/api/v1/roles", session.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var me = await client.SendAsync(HttpMethod.Get, "/api/v1/auth/me", session.AccessToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = (await me.Content.ReadFromJsonAsync<CurrentUserDto>())!;
        profile.Role.ShouldBe(RoleNames.Management);
        profile.Permissions.ShouldNotContain(Permissions.UsersManage);
        profile.Permissions.ShouldContain(Permissions.DashboardView);
    }

    [Fact]
    public async Task Anonymous_requests_to_protected_endpoints_get_401()
    {
        var response = await fixture.CreateClient().GetAsync("/api/v1/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_tokens_rotate_and_reuse_revokes_the_session()
    {
        var username = Unique("refr");
        await CreateUserAsync(username, RoleNames.Management);
        var client = fixture.CreateClient();
        var first = await client.LoginAsync(username, UserPassword);

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = (await rotated.Content.ReadFromJsonAsync<AuthTokensDto>())!;
        second.RefreshToken.ShouldNotBe(first.RefreshToken);

        // Presenting the old token again is treated as theft: it fails and kills the whole family.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var afterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(second.RefreshToken));
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Creating_a_user_validates_input_and_writes_an_audit_entry()
    {
        var admin = await fixture.GetAdminTokenAsync();
        var client = fixture.CreateClient();

        var invalid = await client.SendAsync(HttpMethod.Post, "/api/v1/users", admin,
            new CreateUserRequest("x", "", null, "Nope", "short"));
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await invalid.Content.ReadAsStringAsync();
        body.ShouldContain("InitialPassword");

        var username = Unique("audit");
        var created = await CreateUserAsync(username, RoleNames.Management);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var entry = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.EntityName == "User" && a.Action == "Create" && a.EntityId == created.Id.ToString());
        entry.ShouldNotBeNull();
        entry.NewValues.ShouldNotBeNull();
        entry.NewValues.ShouldContain(username);
        entry.NewValues.ShouldNotContain("PasswordHash"); // secrets are never audited
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_deactivated_and_usernames_are_unique()
    {
        var admin = await fixture.GetAdminTokenAsync();
        var client = fixture.CreateClient();

        var me = await client.SendAsync(HttpMethod.Get, "/api/v1/auth/me", admin);
        var self = (await me.Content.ReadFromJsonAsync<CurrentUserDto>())!;
        var deactivateSelf = await client.SendAsync(HttpMethod.Post, $"/api/v1/users/{self.Id}/deactivate", admin);
        deactivateSelf.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var username = Unique("dupe");
        await CreateUserAsync(username, RoleNames.Management);
        var duplicate = await client.SendAsync(HttpMethod.Post, "/api/v1/users", admin,
            new CreateUserRequest(username, "Dupe", null, RoleNames.Management, UserPassword));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
