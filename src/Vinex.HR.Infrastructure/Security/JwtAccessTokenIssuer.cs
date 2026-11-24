using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Domain.Entities;

namespace Vinex.HR.Infrastructure.Security;

public static class AppClaims
{
    public const string UserId = "sub";
    public const string Username = "name";
    public const string Role = "role";
    public const string MustChangePassword = "must_change_password";
}

internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> jwt, IOptions<AuthOptions> auth, IClock clock)
    : IAccessTokenIssuer
{
    public AccessToken Issue(User user, string roleName)
    {
        var options = jwt.Value;
        var now = clock.UtcNow;
        var expires = now.AddMinutes(auth.Value.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, user.Id.ToString()),
                new Claim(AppClaims.Username, user.Username),
                new Claim(AppClaims.Role, roleName),
                new Claim(AppClaims.MustChangePassword, user.MustChangePassword ? "true" : "false"),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, new DateTimeOffset(expires, TimeSpan.Zero));
    }
}
