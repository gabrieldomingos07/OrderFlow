using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace OrderFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in memory against a throw-away PostgreSQL container, so the tests exercise
/// the same SQL, migrations, constraints and concurrency checks that run in production.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@orderflow.test";
    public const string AdminPassword = "Admin@12345";
    public const string JwtIssuer = "OrderFlow.Tests";
    public const string JwtAudience = "OrderFlow.Tests.Clients";
    // 64+ characters, so the same key can also sign an HS512 token in the algorithm-pinning test.
    public const string JwtSigningKey = "integration-tests-signing-key-0123456789abcdef-0123456789abcdef-x";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await _database.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Added last, so these values win over appsettings.json and environment variables.
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _database.GetConnectionString(),
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:SecretKey"] = JwtSigningKey,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Database:SeedSampleProducts"] = "false",
                ["Database:AdminEmail"] = AdminEmail,
                ["Database:AdminPassword"] = AdminPassword,
                ["RateLimiting:Auth:PermitLimit"] = "100000",
                ["Serilog:MinimumLevel:Default"] = "Warning",
            }));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
