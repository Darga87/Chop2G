using Chop.Application.Alerts;
using Chop.Application.Guards;
using Chop.Application.Incidents;
using Chop.Application.Platform;
using Chop.Infrastructure.Alerts;
using Chop.Infrastructure.Guards;
using Chop.Infrastructure.Incidents;
using Chop.Infrastructure.Persistence;
using Chop.Infrastructure.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Chop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseProvider = configuration["Database:Provider"] ?? "Postgres";
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // On Windows, "localhost" can resolve to IPv6 ::1 first; some setups block that socket path.
        // Normalize to IPv4 loopback to avoid dev-time connectivity issues.
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = connectionString
                .Replace("Host=localhost", "Host=127.0.0.1", StringComparison.OrdinalIgnoreCase)
                .Replace("Server=localhost", "Server=127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(
                    connectionString ?? "Data Source=chop2g.db",
                    sqlite => sqlite.UseNetTopologySuite());
                return;
            }

            options.UseNpgsql(
                connectionString ?? "Host=127.0.0.1;Port=5432;Database=chop2g;Username=postgres;Password=postgres",
                npgsql => npgsql.UseNetTopologySuite());
        });
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IIncidentIdempotencyRepository, IncidentIdempotencyRepository>();
        services.AddScoped<IGuardLocationService, GuardLocationService>();
        services.AddScoped<IAlertNotificationService, AlertNotificationService>();
        services.AddScoped<IAlertEventsService, AlertEventsService>();
        services.AddScoped<IPlatformOutboxService, PlatformOutboxService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHostedService<IncidentIdempotencyCleanupService>();

        return services;
    }
}
