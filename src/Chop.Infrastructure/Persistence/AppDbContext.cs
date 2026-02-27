using Chop.Domain.Auth;
using Chop.Domain.Alerts;
using Chop.Domain.Clients;
using Chop.Domain.Geo;
using Chop.Domain.Guards;
using Chop.Domain.Incidents;
using Chop.Domain.Payments;
using Chop.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Chop.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentStatusHistory> IncidentStatusHistory => Set<IncidentStatusHistory>();

    public DbSet<Dispatch> Dispatches => Set<Dispatch>();

    public DbSet<DispatchRecipient> DispatchRecipients => Set<DispatchRecipient>();

    public DbSet<IncidentAssignment> IncidentAssignments => Set<IncidentAssignment>();

    public DbSet<IncidentIdempotency> IncidentIdempotencies => Set<IncidentIdempotency>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();

    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();

    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();

    public DbSet<ClientPhone> ClientPhones => Set<ClientPhone>();

    public DbSet<ClientAddress> ClientAddresses => Set<ClientAddress>();
    public DbSet<BillingTariff> BillingTariffs => Set<BillingTariff>();
    public DbSet<BankImport> BankImports => Set<BankImport>();
    public DbSet<BankImportRow> BankImportRows => Set<BankImportRow>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<GuardLocation> GuardLocations => Set<GuardLocation>();

    public DbSet<GuardGroup> GuardGroups => Set<GuardGroup>();

    public DbSet<GuardGroupMember> GuardGroupMembers => Set<GuardGroupMember>();

    public DbSet<SecurityPoint> SecurityPoints => Set<SecurityPoint>();

    public DbSet<GuardShift> GuardShifts => Set<GuardShift>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SyncGeoCompatibilityFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SyncGeoCompatibilityFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            modelBuilder.Entity<Incident>()
                .Property(x => x.GeoPoint)
                .HasColumnType("geography (point,4326)");

            modelBuilder.Entity<ClientAddress>()
                .Property(x => x.GeoPoint)
                .HasColumnType("geography (point,4326)");

            modelBuilder.Entity<GuardLocation>()
                .Property(x => x.GeoPoint)
                .HasColumnType("geography (point,4326)");

            modelBuilder.Entity<SecurityPoint>()
                .Property(x => x.GeoPoint)
                .HasColumnType("geography (point,4326)");
        }
    }

    private void SyncGeoCompatibilityFields()
    {
        SyncIncidents();
        SyncClientAddresses();
        SyncGuardLocations();
        SyncSecurityPoints();
    }

    private void SyncIncidents()
    {
        foreach (var entry in ChangeTracker.Entries<Incident>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var entity = entry.Entity;
            if (entity.GeoPoint is not null)
            {
                var (lat, lon) = GeoPointHelper.Read(entity.GeoPoint, entity.Latitude, entity.Longitude);
                entity.Latitude = lat;
                entity.Longitude = lon;
            }
            else
            {
                entity.GeoPoint = GeoPointHelper.Create(entity.Latitude, entity.Longitude);
            }
        }
    }

    private void SyncClientAddresses()
    {
        foreach (var entry in ChangeTracker.Entries<ClientAddress>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var entity = entry.Entity;
            if (entity.GeoPoint is not null)
            {
                var (lat, lon) = GeoPointHelper.Read(entity.GeoPoint, entity.Latitude, entity.Longitude);
                entity.Latitude = lat;
                entity.Longitude = lon;
            }
            else
            {
                entity.GeoPoint = GeoPointHelper.Create(entity.Latitude, entity.Longitude);
            }
        }
    }

    private void SyncGuardLocations()
    {
        foreach (var entry in ChangeTracker.Entries<GuardLocation>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var entity = entry.Entity;
            if (entity.GeoPoint is not null)
            {
                var (lat, lon) = GeoPointHelper.Read(entity.GeoPoint, entity.Latitude, entity.Longitude);
                if (lat.HasValue && lon.HasValue)
                {
                    entity.Latitude = lat.Value;
                    entity.Longitude = lon.Value;
                }
            }
            else
            {
                entity.GeoPoint = GeoPointHelper.Create(entity.Latitude, entity.Longitude);
            }
        }
    }

    private void SyncSecurityPoints()
    {
        foreach (var entry in ChangeTracker.Entries<SecurityPoint>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var entity = entry.Entity;
            if (entity.GeoPoint is not null)
            {
                var (lat, lon) = GeoPointHelper.Read(entity.GeoPoint, entity.Latitude, entity.Longitude);
                entity.Latitude = lat;
                entity.Longitude = lon;
            }
            else
            {
                entity.GeoPoint = GeoPointHelper.Create(entity.Latitude, entity.Longitude);
            }
        }
    }
}
