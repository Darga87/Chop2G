using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Guards;
using Chop.Shared.Contracts.Incidents;

namespace Chop.Api.Tests;

public sealed class GuardLocationPingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public GuardLocationPingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_AsGuard_Returns204()
    {
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");

        using var pingMessage = new HttpRequestMessage(HttpMethod.Post, "/api/guard/location/ping")
        {
            Content = JsonContent.Create(new GuardLocationPingDto
            {
                Lat = 55.75,
                Lon = 37.61,
                AccuracyM = 10,
                IncidentId = null,
            }),
        };
        pingMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardToken);

        var response = await _client.SendAsync(pingMessage);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Ping_WithIncidentId_FromNonAssignedGuard_Returns403()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var assignedGuardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var secondGuardToken = await LoginAndGetAccessTokenAsync("guard-second", "guard-second-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "guard-ping-auth");

        using var dispatchMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients =
                [
                    new DispatchRecipientInputDto { Type = "GUARD", Id = TestUsers.Guard.UserId },
                ],
            }),
        };
        dispatchMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatchResponse = await _client.SendAsync(dispatchMessage);
        dispatchResponse.EnsureSuccessStatusCode();

        using var assignedPing = CreatePingMessage(assignedGuardToken, incidentId);
        var assignedResponse = await _client.SendAsync(assignedPing);
        Assert.Equal(HttpStatusCode.NoContent, assignedResponse.StatusCode);

        using var secondPing = CreatePingMessage(secondGuardToken, incidentId);
        var secondResponse = await _client.SendAsync(secondPing);
        Assert.Equal(HttpStatusCode.Forbidden, secondResponse.StatusCode);
    }

    private static HttpRequestMessage CreatePingMessage(string accessToken, Guid incidentId)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/guard/location/ping")
        {
            Content = JsonContent.Create(new GuardLocationPingDto
            {
                Lat = 55.75,
                Lon = 37.61,
                AccuracyM = 10,
                IncidentId = incidentId,
            }),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return message;
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
