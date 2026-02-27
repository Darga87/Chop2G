using Chop.Infrastructure.Persistence;
using Chop.Api.Auth;
using Chop.Domain.Auth;
using Chop.Domain.Clients;
using Chop.Domain.Incidents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chop.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"chop2g-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["Auth:PasswordHashing:Iterations"] = "1000",
                ["Notifications:Outbox:PollIntervalMs"] = "100",
                ["Notifications:Outbox:BatchSize"] = "20",
                ["Notifications:Outbox:MaxAttempts"] = "3",
                ["Alerts:Sla:PollIntervalMs"] = "100",
                ["Alerts:Sla:NoAcceptStuckSeconds"] = "1",
                ["Alerts:Sla:GuardOfflineSeconds"] = "1",
                ["Alerts:Sla:StuckInStatusSeconds"] = "1",
                ["Platform:Reliability:OutboxPollIntervalMs"] = "100",
                ["Platform:Reliability:OutboxBatchSize"] = "50",
                ["Platform:Reliability:OutboxMaxAttempts"] = "2",
                ["Platform:Reliability:OutboxRetryBaseSeconds"] = "1",
                ["Platform:Reliability:OutboxRetryMaxSeconds"] = "2",
                ["Platform:Reliability:OutboxLagUnhealthyThresholdSeconds"] = "120",
                ["Platform:Reliability:OutboxRetentionDays"] = "30",
                ["Platform:Reliability:AuditRetentionDays"] = "30",
            };
            configBuilder.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}", sqlite => sqlite.UseNetTopologySuite()));

            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();

            SeedAuthUsers(dbContext);
            dbContext.ClientProfiles.AddRange(
                new ClientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = TestUsers.Client.UserId,
                    FullName = "Client Test",
                },
                new ClientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = TestUsers.ClientDedupKey.UserId,
                    FullName = "Client Dedup Key",
                },
                new ClientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = TestUsers.ClientDedupWindow.UserId,
                    FullName = "Client Dedup Window",
                },
                new ClientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = TestUsers.ClientIdemConflict.UserId,
                    FullName = "Client Idem Conflict",
                },
                new ClientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = TestUsers.ClientConcurrentNoKey.UserId,
                    FullName = "Client Concurrent No Key",
                });

            dbContext.BillingTariffs.AddRange(
                new BillingTariff
                {
                    Code = "STANDARD",
                    Name = "Стандарт",
                    Description = "Базовый тариф",
                    MonthlyFee = 0m,
                    Currency = "KZT",
                    IsActive = true,
                    SortOrder = 10,
                    UpdatedAtUtc = DateTime.UtcNow,
                },
                new BillingTariff
                {
                    Code = "PREMIUM",
                    Name = "Премиум",
                    Description = "Расширенный тариф",
                    MonthlyFee = 0m,
                    Currency = "KZT",
                    IsActive = true,
                    SortOrder = 20,
                    UpdatedAtUtc = DateTime.UtcNow,
                },
                new BillingTariff
                {
                    Code = "VIP",
                    Name = "VIP",
                    Description = "Индивидуальный тариф",
                    MonthlyFee = 0m,
                    Currency = "KZT",
                    IsActive = true,
                    SortOrder = 30,
                    UpdatedAtUtc = DateTime.UtcNow,
                });
            dbContext.SaveChanges();
        });
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task SeedClientProfileAsync(
        string userId,
        string fullName,
        IEnumerable<(string Phone, string Type, bool IsPrimary)> phones,
        IEnumerable<(string Label, string Address, double? Lat, double? Lon, bool IsPrimary)> addresses)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await dbContext.ClientProfiles
            .Include(x => x.Phones)
            .Include(x => x.Addresses)
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (existing is not null)
        {
            existing.FullName = fullName;
            dbContext.ClientPhones.RemoveRange(dbContext.ClientPhones.Where(x => x.ClientProfileId == existing.Id));
            dbContext.ClientAddresses.RemoveRange(dbContext.ClientAddresses.Where(x => x.ClientProfileId == existing.Id));
            dbContext.ClientPhones.AddRange(phones.Select(x => new ClientPhone
            {
                Id = Guid.NewGuid(),
                ClientProfileId = existing.Id,
                Phone = x.Phone,
                Type = x.Type,
                IsPrimary = x.IsPrimary,
            }));
            dbContext.ClientAddresses.AddRange(addresses.Select(x => new ClientAddress
            {
                Id = Guid.NewGuid(),
                ClientProfileId = existing.Id,
                Label = x.Label,
                AddressText = x.Address,
                Latitude = x.Lat,
                Longitude = x.Lon,
                IsPrimary = x.IsPrimary,
            }));
            await dbContext.SaveChangesAsync();
            return;
        }

        var profile = new ClientProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = fullName,
            Phones = phones.Select(x => new ClientPhone
            {
                Id = Guid.NewGuid(),
                Phone = x.Phone,
                Type = x.Type,
                IsPrimary = x.IsPrimary,
            }).ToArray(),
            Addresses = addresses.Select(x => new ClientAddress
            {
                Id = Guid.NewGuid(),
                Label = x.Label,
                AddressText = x.Address,
                Latitude = x.Lat,
                Longitude = x.Lon,
                IsPrimary = x.IsPrimary,
            }).ToArray(),
        };

        dbContext.ClientProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Guid> SeedIncidentAsync(string clientUserId, IncidentStatus status, string? addressText = null, DateTime? createdAtUtc = null, DateTime? lastUpdatedAtUtc = null)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var created = createdAtUtc ?? now;
        var updated = lastUpdatedAtUtc ?? created;
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            ClientUserId = clientUserId,
            Status = status,
            CreatedAtUtc = created,
            LastUpdatedAtUtc = updated,
            AddressText = addressText ?? $"seed-{status}",
            StatusHistory =
            [
                new IncidentStatusHistory
                {
                    Id = Guid.NewGuid(),
                    ToStatus = status,
                    ActorUserId = "seed",
                    ActorRole = "SYSTEM",
                    CreatedAtUtc = updated,
                },
            ],
        };

        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync();
        return incident.Id;
    }

    public new async Task DisposeAsync()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // Ignore best-effort test DB cleanup on Windows file locks.
        }

        await base.DisposeAsync();
    }

    private static void SeedAuthUsers(AppDbContext dbContext)
    {
        var hasher = new PasswordHasher(Options.Create(new PasswordHashOptions { Iterations = 1000 }));
        var now = DateTime.UtcNow;

        foreach (var testUser in TestUsers.All())
        {
            dbContext.Users.Add(new User
            {
                Id = testUser.Id,
                Login = testUser.Login,
                IsActive = true,
                CreatedAtUtc = now,
            });

            dbContext.UserCredentials.Add(new UserCredential
            {
                UserId = testUser.Id,
                PasswordAlgo = "PBKDF2-SHA256",
                PasswordHash = hasher.Hash(testUser.Password),
                PasswordChangedAtUtc = now,
            });

            dbContext.UserRoles.AddRange(testUser.Roles.Select(role => new UserRole
            {
                UserId = testUser.Id,
                Role = role,
            }));
        }
    }
}
