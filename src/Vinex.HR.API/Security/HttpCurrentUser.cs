using System.Security.Claims;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Infrastructure.Security;

namespace Vinex.HR.API.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? UserId => int.TryParse(Principal?.FindFirstValue(AppClaims.UserId), out var id) ? id : null;

    public string? Username => Principal?.FindFirstValue(AppClaims.Username);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
