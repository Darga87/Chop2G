using Chop.Application;
using Chop.Api.Alerts;
using Chop.Api.Auth;
using Chop.Api.Backoffice;
using Chop.Api.Incidents;
using Chop.Api.Platform;
using Chop.Infrastructure;
using Chop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    });

    options.AddPolicy("guard-ping", httpContext =>
    {
        var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var key = string.IsNullOrWhiteSpace(userId) ? "anon" : $"user:{userId}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            // Guard ping is high-frequency; we still cap to protect DB/outbox.
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    });
});
builder.Services.AddHealthChecks()
    .AddCheck<OutboxLagHealthCheck>("outbox_lag");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<PasswordHashOptions>(builder.Configuration.GetSection("Auth:PasswordHashing"));
builder.Services.Configure<NotificationOutboxOptions>(builder.Configuration.GetSection("Notifications:Outbox"));
builder.Services.Configure<AlertSlaOptions>(builder.Configuration.GetSection("Alerts:Sla"));
builder.Services.Configure<PlatformReliabilityOptions>(builder.Configuration.GetSection("Platform:Reliability"));
builder.Services.AddScoped<BackofficePaymentsStore>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IIncidentRealtimePublisher, IncidentRealtimePublisher>();
builder.Services.AddScoped<IOutboxEventPublisher, OutboxSignalREventPublisher>();
builder.Services.AddHostedService<NotificationOutboxDispatcher>();
builder.Services.AddHostedService<IncidentAlertSlaWorker>();
builder.Services.AddHostedService<OutboxMessageProcessor>();
builder.Services.AddHostedService<PlatformRetentionCleanupService>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (!builder.Environment.IsDevelopment() && JwtOptions.IsUnsafeSigningKey(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "JWT signing key is not configured for non-Development environment. Configure a strong secret key.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
    {
        dbContext.Database.Migrate();
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<IncidentsHub>("/hubs/incidents");

app.Run();

public partial class Program;
