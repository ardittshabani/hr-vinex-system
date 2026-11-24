using Vinex.HR.Contracts.Auth;
using Vinex.HR.Desktop.Core;

namespace Vinex.HR.Desktop.Services;

public sealed class AuthApi(AnonymousApiClient anonymous, ApiClient authenticated)
{
    public Task<AuthTokensDto> LoginAsync(string username, string password, CancellationToken ct = default) =>
        anonymous.PostAsync<AuthTokensDto>("api/v1/auth/login", new LoginRequest(username, password), ct);

    public Task LogoutAsync(string? refreshToken, CancellationToken ct = default) =>
        authenticated.PostAsync("api/v1/auth/logout", new LogoutRequest(refreshToken), ct);

    public Task<AuthTokensDto> ChangePasswordAsync(string current, string next, CancellationToken ct = default) =>
        authenticated.PostAsync<AuthTokensDto>("api/v1/auth/change-password", new ChangePasswordRequest(current, next), ct);
}
