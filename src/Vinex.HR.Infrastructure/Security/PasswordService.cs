using Microsoft.AspNetCore.Identity;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Domain.Entities;

namespace Vinex.HR.Infrastructure.Security;

/// <summary>PBKDF2 password hashing via ASP.NET Core's PasswordHasher (versioned format, supports rehash).</summary>
internal sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();
    private static readonly User Dummy = new();

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public bool Verify(string hash, string password, out bool needsRehash)
    {
        var result = _hasher.VerifyHashedPassword(Dummy, hash, password);
        needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }
}
