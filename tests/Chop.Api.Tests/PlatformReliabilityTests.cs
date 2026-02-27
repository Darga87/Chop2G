using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chop.Api.Tests;

public sealed class PlatformReliabilityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public PlatformReliabilityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IncidentCreated_WritesPlatformOutboxMessage_AndGetsPublished()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        var beforeCount = await CountOutboxMessagesAsync();

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto { AddressText = "platform-outbox-test" }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();

        await WaitForOutboxIncreaseAndPublishAsync(beforeCount, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UnsupportedOutboxEventType_EventEventuallyMovesToFailed()
    {
        Guid messageId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                AggregateType = "test",
                AggregateId = Guid.NewGuid(),
                EventType = "unsupported.event",
                PayloadJson = "{}",
                Status = OutboxMessageStatus.Pending,
                AttemptCount = 0,
                NextAttemptAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
            };
            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();
            messageId = message.Id;
        }

        await WaitForOutboxFailedAsync(messageId, TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task Login_WritesAuditLogEntry()
    {
        var beforeCount = await CountAuditEntriesAsync();
        _ = await LoginAndGetAccessTokenAsync("operator", "operator-pass");

        var afterCount = await CountAuditEntriesAsync();
        Assert.True(afterCount > beforeCount);
    }

    private async Task<int> CountOutboxMessagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.OutboxMessages.CountAsync();
    }

    private async Task<int> CountAuditEntriesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.AuditLogEntries.CountAsync();
    }

    private async Task WaitForOutboxIncreaseAndPublishAsync(int beforeCount, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var total = await dbContext.OutboxMessages.CountAsync();
            if (total > beforeCount)
            {
                var latest = await dbContext.OutboxMessages
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync();
                Assert.NotNull(latest);
                Assert.Equal("incident", latest!.AggregateType);
                if (latest.Status == OutboxMessageStatus.Published)
                {
                    return;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Timed out waiting for platform outbox message.");
    }

    private async Task WaitForOutboxFailedAsync(Guid messageId, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = await dbContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == messageId);
            if (message is not null && message.Status == OutboxMessageStatus.Failed)
            {
                Assert.True(message.AttemptCount >= 2);
                Assert.False(string.IsNullOrWhiteSpace(message.LastError));
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Timed out waiting for outbox message to fail.");
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
