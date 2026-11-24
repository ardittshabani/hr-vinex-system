namespace Vinex.HR.Domain.Enums;

public enum AuditAction
{
    Create,
    Update,
    Delete,
    Login,
    LoginFailed,
    AccountLocked,
    Logout,
    PasswordChanged,
    PasswordReset,
    TokenReuseDetected,
    Activated,
    Deactivated,
}
