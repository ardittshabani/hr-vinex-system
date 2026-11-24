using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Application.Common;
using Vinex.HR.Contracts.Auth;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Domain.Entities;
using Vinex.HR.Domain.Enums;

namespace Vinex.HR.Application.Auth;

public interface IAuthService
{
    Task<AuthTokensDto> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthTokensDto> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task LogoutAsync(string? refreshToken, CancellationToken ct);
    Task<AuthTokensDto> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct);
    Task<CurrentUserDto> GetCurrentUserAsync(int userId, CancellationToken ct);
}

internal sealed class AuthService(
    IHrDbContext db,
    IPasswordService passwords,
    IAccessTokenIssuer tokenIssuer,
    IAuditLogger audit,
    IClock clock,
    IOptions<AuthOptions> options) : IAuthService
{
    private readonly AuthOptions _options = options.Value;

    private static AppException InvalidCredentials() =>
        AppException.Unauthorized("invalid_credentials", "Invalid username or password.");

    public async Task<AuthTokensDto> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var username = request.Username.Trim();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username, ct);

        if (user is null)
        {
            audit.Log(AuditAction.LoginFailed, nameof(User), null, $"Failed login for unknown user '{Short(username)}'.");
            await db.SaveChangesAsync(ct);
            throw InvalidCredentials();
        }

        if (!user.IsActive)
        {
            audit.Log(AuditAction.LoginFailed, nameof(User), user.Id.ToString(), $"Login refused: user '{user.Username}' is deactivated.", userId: user.Id);
            await db.SaveChangesAsync(ct);
            throw InvalidCredentials();
        }

        if (user.IsLockedOut(now))
        {
            audit.Log(AuditAction.LoginFailed, nameof(User), user.Id.ToString(), $"Login refused: user '{user.Username}' is locked out.", userId: user.Id);
            await db.SaveChangesAsync(ct);
            throw AppException.Unauthorized("account_locked", "Too many failed attempts. Try again later.");
        }

        if (!passwords.Verify(user.PasswordHash, request.Password, out var needsRehash))
        {
            user.FailedLoginCount++;
            audit.Log(AuditAction.LoginFailed, nameof(User), user.Id.ToString(), $"Failed login for '{user.Username}'.", userId: user.Id);
            if (user.FailedLoginCount >= _options.MaxFailedLogins)
            {
                user.LockoutEndAt = now.AddMinutes(_options.LockoutMinutes);
                user.FailedLoginCount = 0;
                audit.Log(AuditAction.AccountLocked, nameof(User), user.Id.ToString(), $"User '{user.Username}' locked for {_options.LockoutMinutes} minutes.", userId: user.Id);
            }

            await db.SaveChangesAsync(ct);
            throw InvalidCredentials();
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now;
        if (needsRehash)
        {
            user.PasswordHash = passwords.Hash(request.Password);
        }

        audit.Log(AuditAction.Login, nameof(User), user.Id.ToString(), $"User '{user.Username}' signed in.", userId: user.Id);
        var tokens = IssueTokens(user, now, out var refreshEntity);
        db.RefreshTokens.Add(refreshEntity);
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task<AuthTokensDto> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            throw AppException.Unauthorized("invalid_refresh_token", "Session expired. Please sign in again.");
        }

        if (stored.RevokedAt is not null)
        {
            // A revoked token being presented again suggests theft: revoke the whole family.
            var active = await db.RefreshTokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in active)
            {
                t.RevokedAt = now;
            }

            audit.Log(AuditAction.TokenReuseDetected, nameof(User), stored.UserId.ToString(),
                $"Refresh token reuse detected for '{stored.User.Username}'; all sessions revoked.", userId: stored.UserId);
            await db.SaveChangesAsync(ct);
            throw AppException.Unauthorized("invalid_refresh_token", "Session expired. Please sign in again.");
        }

        if (!stored.IsActive(now) || !stored.User.IsActive)
        {
            throw AppException.Unauthorized("invalid_refresh_token", "Session expired. Please sign in again.");
        }

        var tokens = IssueTokens(stored.User, now, out var replacement);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct); // assigns replacement.Id
        stored.RevokedAt = now;
        stored.ReplacedByTokenId = replacement.Id;
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = clock.UtcNow;
        audit.Log(AuditAction.Logout, nameof(User), stored.UserId.ToString(), $"User '{stored.User.Username}' signed out.", userId: stored.UserId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthTokensDto> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw AppException.NotFound("User");

        if (!passwords.Verify(user.PasswordHash, request.CurrentPassword, out _))
        {
            throw AppException.Validation(nameof(ChangePasswordRequest.CurrentPassword), "Current password is incorrect.");
        }

        user.PasswordHash = passwords.Hash(request.NewPassword);
        user.MustChangePassword = false;

        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active)
        {
            t.RevokedAt = now;
        }

        audit.Log(AuditAction.PasswordChanged, nameof(User), user.Id.ToString(), $"User '{user.Username}' changed their password.", userId: user.Id);
        var tokens = IssueTokens(user, now, out var refreshEntity);
        db.RefreshTokens.Add(refreshEntity);
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Role).AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw AppException.NotFound("User");
        return ToCurrentUser(user);
    }

    private AuthTokensDto IssueTokens(User user, DateTime now, out RefreshToken refreshEntity)
    {
        var access = tokenIssuer.Issue(user, user.Role.Name);
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expires = now.AddDays(_options.RefreshTokenDays);
        refreshEntity = new RefreshToken
        {
            UserId = user.Id,
            User = user,
            TokenHash = HashToken(raw),
            CreatedAt = now,
            ExpiresAt = expires,
        };
        return new AuthTokensDto(access.Token, access.ExpiresAt, raw, new DateTimeOffset(expires, TimeSpan.Zero), ToCurrentUser(user));
    }

    internal static CurrentUserDto ToCurrentUser(User user) => new(
        user.Id,
        user.Username,
        user.DisplayName,
        user.Role.Name,
        user.MustChangePassword,
        RolePermissions.For(user.Role.Name).Order().ToList());

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Short(string s) => s.Length <= 50 ? s : s[..50];
}
