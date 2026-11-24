using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vinex.HR.Application.Common;

namespace Vinex.HR.API.Infrastructure;

/// <summary>Maps exceptions to RFC 9457 problem responses. Unexpected errors expose only a trace id.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case AppException app:
                var status = app.Kind switch
                {
                    AppErrorKind.Validation => StatusCodes.Status400BadRequest,
                    AppErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
                    AppErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                    AppErrorKind.NotFound => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status409Conflict,
                };
                problem = app.Errors is null
                    ? new ProblemDetails()
                    : new ValidationProblemDetails(app.Errors.ToDictionary(e => e.Key, e => e.Value));
                problem.Status = status;
                problem.Title = app.Message;
                problem.Extensions["code"] = app.Code;
                break;

            case DbUpdateConcurrencyException:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "This record was changed by someone else. Reload it and try again.",
                };
                problem.Extensions["code"] = "concurrency_conflict";
                break;

            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                return true; // client went away

            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                };
                problem.Extensions["code"] = "server_error";
                break;
        }

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
