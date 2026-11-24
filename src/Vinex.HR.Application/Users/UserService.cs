using Microsoft.EntityFrameworkCore;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Application.Common;
using Vinex.HR.Contracts.Common;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Contracts.Users;
using Vinex.HR.Domain.Entities;
using Vinex.HR.Domain.Enums;

namespace Vinex.HR.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<UserDto> GetAsync(int id, CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct);
    Task ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken ct);
    Task SetActiveAsync(int id, bool active, CancellationToken ct);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct);
}

internal sealed class UserService(
    IHrDbContext db,
    IPasswordService passwords,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock) : IUserService
{
    public async Task<PagedResult<UserDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Users.AsNoTracking().Include(u => u.Role).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.Username.Contains(s) || u.DisplayName.Contains(s) || (u.Email != null && u.Email.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<UserDto>(users.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<UserDto> GetAsync(int id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw AppException.NotFound("User");
        return ToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username, ct))
        {
            throw new AppException(AppErrorKind.Conflict, "username_taken", "That username is already in use.",
                new Dictionary<string, string[]> { [nameof(CreateUserRequest.Username)] = ["That username is already in use."] });
        }

        var role = await FindRoleAsync(request.Role, ct);
        var user = new User
        {
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            Email = NullIfEmpty(request.Email),
            RoleId = role.Id,
            Role = role,
            PasswordHash = passwords.Hash(request.InitialPassword),
            MustChangePassword = true,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw AppException.NotFound("User");

        db.Entry(user).Property(u => u.RowVersion).OriginalValue = DecodeRowVersion(request.RowVersion);

        var role = await FindRoleAsync(request.Role, ct);
        if (user.RoleId != role.Id && user.Role.Name == RoleNames.HRAdministrator)
        {
            await EnsureNotLastAdminAsync(user, ct);
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = NullIfEmpty(request.Email);
        user.RoleId = role.Id;
        user.Role = role;
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("User");
        user.PasswordHash = passwords.Hash(request.NewPassword);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        await RevokeSessionsAsync(user.Id, ct);
        audit.Log(AuditAction.PasswordReset, nameof(User), user.Id.ToString(), $"Password reset for user '{user.Username}'.");
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw AppException.NotFound("User");
        if (user.IsActive == active)
        {
            return;
        }

        if (!active)
        {
            if (currentUser.UserId == user.Id)
            {
                throw AppException.Conflict("cannot_deactivate_self", "You cannot deactivate your own account.");
            }

            if (user.Role.Name == RoleNames.HRAdministrator)
            {
                await EnsureNotLastAdminAsync(user, ct);
            }

            await RevokeSessionsAsync(user.Id, ct);
        }

        user.IsActive = active;
        audit.Log(active ? AuditAction.Activated : AuditAction.Deactivated, nameof(User), user.Id.ToString(),
            $"User '{user.Username}' {(active ? "activated" : "deactivated")}.");
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        return roles.Select(r => new RoleDto(r.Id, r.Name, RoleNames.Display(r.Name))).ToList();
    }

    private async Task<Role> FindRoleAsync(string name, CancellationToken ct) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Name == name, ct)
        ?? throw AppException.Validation(nameof(CreateUserRequest.Role), "Select a valid role.");

    private async Task EnsureNotLastAdminAsync(User user, CancellationToken ct)
    {
        var otherActiveAdmins = await db.Users.CountAsync(
            u => u.Id != user.Id && u.IsActive && u.Role.Name == RoleNames.HRAdministrator, ct);
        if (otherActiveAdmins == 0)
        {
            throw AppException.Conflict("last_admin", "At least one active HR Administrator must remain.");
        }
    }

    private async Task RevokeSessionsAsync(int userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens)
        {
            t.RevokedAt = now;
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static byte[] DecodeRowVersion(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw AppException.Validation(nameof(UpdateUserRequest.RowVersion), "Invalid row version.");
        }
    }

    private static UserDto ToDto(User u) => new(
        u.Id,
        u.Username,
        u.DisplayName,
        u.Email,
        u.Role.Name,
        u.IsActive,
        u.MustChangePassword,
        u.LastLoginAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(u.LastLoginAt.Value, DateTimeKind.Utc)),
        Convert.ToBase64String(u.RowVersion));
}
