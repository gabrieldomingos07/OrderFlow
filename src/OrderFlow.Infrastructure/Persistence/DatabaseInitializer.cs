using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.Users;

namespace OrderFlow.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Applies pending migrations when the application starts. Handy for local development and
    /// demos; in production prefer running an EF Core migration bundle as a deployment step.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Inserts a few sample products when the catalog is empty.</summary>
    public bool SeedSampleProducts { get; set; }

    /// <summary>When both are set, an administrator with these credentials is created if missing.</summary>
    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}

public static partial class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var dbContext = provider.GetRequiredService<OrderFlowDbContext>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (options.ApplyMigrationsOnStartup)
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            LogMigrationsApplied(logger);
        }

        if (!string.IsNullOrWhiteSpace(options.AdminEmail) && !string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            var email = User.NormalizeEmail(options.AdminEmail);
            if (!await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
            {
                var passwordHasher = provider.GetRequiredService<IPasswordHasher>();
                dbContext.Users.Add(User.Create(
                    email,
                    "OrderFlow Administrator",
                    passwordHasher.Hash(options.AdminPassword),
                    UserRole.Admin,
                    timeProvider.GetUtcNow()));
                LogAdminSeeded(logger, email);
            }
        }

        if (options.SeedSampleProducts && !await dbContext.Products.AnyAsync(cancellationToken))
        {
            dbContext.Products.AddRange(CreateSampleProducts(timeProvider.GetUtcNow()));
            LogSampleProductsSeeded(logger);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another instance starting at the same time seeded the same data first: nothing to do.
            LogSeedSkipped(logger);
        }
    }

    private static IEnumerable<Product> CreateSampleProducts(DateTimeOffset now)
    {
        (string Sku, string Name, string Description, decimal Price, int Stock)[] samples =
        [
            ("KB-MECH-01", "Mechanical Keyboard", "Hot-swappable switches, ABNT2 layout.", 459.90m, 25),
            ("MS-WL-02", "Wireless Mouse", "Ergonomic, 4000 DPI, USB-C charging.", 189.90m, 40),
            ("MN-27-4K", "27\" 4K Monitor", "IPS panel, 60 Hz, USB-C with 65 W power delivery.", 2199.00m, 8),
            ("HS-ANC-03", "Noise Cancelling Headset", "Bluetooth 5.3 with 30 hours of battery.", 899.00m, 15),
            ("DK-USB-C", "USB-C Docking Station", "Dual HDMI, Ethernet and 100 W passthrough.", 649.50m, 12),
        ];

        foreach (var sample in samples)
        {
            yield return Product.Create(sample.Sku, sample.Name, sample.Description, sample.Price, sample.Stock, now).Value;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Administrator {Email} created")]
    private static partial void LogAdminSeeded(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample products added to the catalog")]
    private static partial void LogSampleProductsSeeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed data was already created by another instance")]
    private static partial void LogSeedSkipped(ILogger logger);
}
