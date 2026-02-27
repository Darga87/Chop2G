using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Guards;
using Chop.Shared.Contracts.Incidents;

namespace Chop.Api.Tests;

public sealed class IncidentNearestTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public IncidentNearestTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Nearest_ForOperator_ReturnsSortedPatrolUnits()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto
            {
                ClientLocation = new IncidentLocationDto { Lat = 55.75, Lon = 37.61 },
                AddressText = "nearest-test",
            }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResponseDto>();
        Assert.NotNull(created);

        using var dispatchMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{created!.IncidentId}/dispatch")
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

        await SendGuardPingAsync(guardToken, created.IncidentId, 55.7501, 37.6101);

        using var nearestRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{created.IncidentId}/nearest?limitUnits=5&limitPosts=5");
        nearestRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var nearestResponse = await _client.SendAsync(nearestRequest);
        nearestResponse.EnsureSuccessStatusCode();
        var payload = await nearestResponse.Content.ReadFromJsonAsync<NearestForcesDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.PatrolUnits);
        Assert.True(payload.PatrolUnits.All(x => x.DistanceMeters >= 0));
    }

    [Fact]
    public async Task Nearest_WithoutToken_Returns401()
    {
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "nearest-no-auth");
        var response = await _client.GetAsync($"/api/operator/incidents/{incidentId}/nearest");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Nearest_WithClientRole_Returns403()
    {
        var token = await LoginAndGetAccessTokenAsync("client", "client-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "nearest-forbidden");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/operator/incidents/{incidentId}/nearest");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SendGuardPingAsync(string token, Guid incidentId, double lat, double lon)
    {
        using var pingMessage = new HttpRequestMessage(HttpMethod.Post, "/api/guard/location/ping")
        {
            Content = JsonContent.Create(new GuardLocationPingDto
            {
                IncidentId = incidentId,
                Lat = lat,
                Lon = lon,
                AccuracyM = 8,
            }),
        };
        pingMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var pingResponse = await _client.SendAsync(pingMessage);
        Assert.Equal(HttpStatusCode.NoContent, pingResponse.StatusCode);
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
