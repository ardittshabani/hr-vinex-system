namespace Vinex.HR.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC signing key. Must be at least 32 characters; supplied via environment/secret store, never committed.</summary>
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "vinex-hr";
    public string Audience { get; set; } = "vinex-hr-desktop";
}
