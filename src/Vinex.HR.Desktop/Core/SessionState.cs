using Vinex.HR.Contracts.Auth;

namespace Vinex.HR.Desktop.Core;

/// <summary>Holds the signed-in user and tokens (in memory only). UI permission checks are conveniences; the API enforces access.</summary>
public sealed class SessionState
{
    private readonly object _lock = new();

    public CurrentUserDto? User { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }

    public bool IsSignedIn => User is not null;

    public event Action? SignedIn;
    public event Action<string?>? SignedOut;

    public void SignIn(AuthTokensDto tokens)
    {
        UpdateTokens(tokens);
        SignedIn?.Invoke();
    }

    public void UpdateTokens(AuthTokensDto tokens)
    {
        lock (_lock)
        {
            User = tokens.User;
            AccessToken = tokens.AccessToken;
            RefreshToken = tokens.RefreshToken;
        }
    }

    public void SignOut(string? reason = null)
    {
        bool wasSignedIn;
        lock (_lock)
        {
            wasSignedIn = User is not null;
            User = null;
            AccessToken = null;
            RefreshToken = null;
        }

        if (wasSignedIn)
        {
            SignedOut?.Invoke(reason);
        }
    }

    public bool Can(string permission) => User?.Permissions.Contains(permission) == true;
}
