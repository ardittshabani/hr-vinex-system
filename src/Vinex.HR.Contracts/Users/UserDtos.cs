namespace Vinex.HR.Contracts.Users;

public sealed record UserDto(
    int Id,
    string Username,
    string DisplayName,
    string? Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAt,
    string RowVersion);

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    string? Email,
    string Role,
    string InitialPassword);

public sealed record UpdateUserRequest(
    string DisplayName,
    string? Email,
    string Role,
    string RowVersion);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record RoleDto(int Id, string Name, string DisplayName);
