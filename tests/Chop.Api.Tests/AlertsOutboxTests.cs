using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Domain.Alerts;
using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chop.Api.Tests;

public sealed class AlertsOutboxTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AlertsOutboxTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IncidentCreated_PersistsAlertAndPlatformOutboxMessage()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto { AddressText = "alerts-outbox-test" }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        await WaitForOutboxPublishAsync(created!.IncidentId, TimeSpan.FromSeconds(5));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alertEvent = await dbContext.AlertEvents
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.RuleCode == "INCIDENT_CREATED" && x.EntityId == created.IncidentId);

        Assert.NotNull(alertEvent);
        Assert.Equal(AlertSeverity.Info, alertEvent!.Severity);
        Assert.Equal(AlertEntityType.Incident, alertEvent.EntityType);
        Assert.Equal(AlertEventStatus.Resolved, alertEvent.Status);

        var published = await dbContext.OutboxMessages
            .AnyAsync(x => x.AggregateId == created.IncidentId
                           && x.EventType == "realtime.incident-created"
                           && x.Status == OutboxMessageStatus.Published);
        Assert.True(published);
    }

    private async Task WaitForOutboxPublishAsync(Guid incidentId, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var published = await dbContext.OutboxMessages
                .AnyAsync(x => x.AggregateId == incidentId
                               && x.EventType == "realtime.incident-created"
                               && x.Status == OutboxMessageStatus.Published);
            if (published)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Timed out waiting for outbox publish.");
    }

    private async Task<string> LoginAndGetAccessTokenAsync(string login, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Login = login,
            Password = password,
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }
}
