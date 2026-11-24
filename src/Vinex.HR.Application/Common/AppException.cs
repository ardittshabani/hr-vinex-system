namespace Vinex.HR.Application.Common;

public enum AppErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

/// <summary>Expected business failure. The API maps it to an RFC 9457 problem response.</summary>
public sealed class AppException : Exception
{
    public AppErrorKind Kind { get; }
    public string Code { get; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public AppException(AppErrorKind kind, string code, string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Kind = kind;
        Code = code;
        Errors = errors;
    }

    public static AppException NotFound(string what) =>
        new(AppErrorKind.NotFound, "not_found", $"{what} was not found.");

    public static AppException Conflict(string code, string message) =>
        new(AppErrorKind.Conflict, code, message);

    public static AppException Unauthorized(string code, string message) =>
        new(AppErrorKind.Unauthorized, code, message);

    public static AppException Validation(string field, string message) =>
        new(AppErrorKind.Validation, "validation_failed", message,
            new Dictionary<string, string[]> { [field] = [message] });
}
