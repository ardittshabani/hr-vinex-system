using Microsoft.Extensions.DependencyInjection;
using Vinex.HR.Application.Abstractions;
using Vinex.HR.Application.Auth;
using Vinex.HR.Application.Common;
using Vinex.HR.Application.Users;

namespace Vinex.HR.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
