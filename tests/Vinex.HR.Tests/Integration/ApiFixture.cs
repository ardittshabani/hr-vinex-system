using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Vinex.HR.Tests.Integration;

/// <summary>Starts a real SQL Server in Docker and boots the API against it (the schema relies on SQL Server behaviour).</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminUsername = "admin";
    public const string AdminInitialPassword = "Initial-Admin-Pass-1";
    public const string AdminPassword = "Changed-Admin-Pass-2";

    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private string? _adminToken;

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Not initialized");

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        var cs = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "VinexHR_Test" }.ConnectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", cs);
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-0123456789-abcdef");
        Environment.SetEnvironmentVariable("Bootstrap__AdminUsername", AdminUsername);
        Environment.SetEnvironmentVariable("Bootstrap__AdminPassword", AdminInitialPassword);
        Environment.SetEnvironmentVariable("Database__AllowEnsureCreated", "true");
        Environment.SetEnvironmentVariable("RateLimit__AuthPermitPerMinute", "100000");

        _factory = new WebApplicationFactory<Program>();
        _ = _factory.Server; // boot the host (runs migrations/seed)
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _sql.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>Signs in as the bootstrap admin, completing the forced password change once.</summary>
    public async Task<string> GetAdminTokenAsync()
    {
        if (_adminToken is not null)
        {
            return _adminToken;
        }

        var client = CreateClient();
        var login = await client.LoginAsync(AdminUsername, AdminInitialPassword);
        login.User.MustChangePassword.ShouldBeTrue();
        var changed = await client.ChangePasswordAsync(login.AccessToken, AdminInitialPassword, AdminPassword);
        _adminToken = changed.AccessToken;
        return _adminToken;
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
