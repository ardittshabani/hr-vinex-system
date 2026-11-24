using Vinex.HR.Contracts.Common;
using Vinex.HR.Contracts.Users;
using Vinex.HR.Desktop.Core;

namespace Vinex.HR.Desktop.Services;

public sealed class UsersApi(ApiClient api)
{
    public Task<PagedResult<UserDto>> ListAsync(string? search, CancellationToken ct = default) =>
        api.GetAsync<PagedResult<UserDto>>(
            $"api/v1/users?pageSize=200&search={Uri.EscapeDataString(search ?? string.Empty)}", ct);

    public Task<IReadOnlyList<RoleDto>> RolesAsync(CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<RoleDto>>("api/v1/roles", ct);

    public Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default) =>
        api.PostAsync<UserDto>("api/v1/users", request, ct);

    public Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default) =>
        api.PutAsync<UserDto>($"api/v1/users/{id}", request, ct);

    public Task ResetPasswordAsync(int id, string newPassword, CancellationToken ct = default) =>
        api.PostAsync($"api/v1/users/{id}/reset-password", new ResetPasswordRequest(newPassword), ct);

    public Task SetActiveAsync(int id, bool active, CancellationToken ct = default) =>
        api.PostAsync($"api/v1/users/{id}/{(active ? "activate" : "deactivate")}", null, ct);
}
