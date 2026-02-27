using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Domain.Incidents;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Incidents;

namespace Chop.Api.Tests;

public sealed class IncidentStatusTransitionsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public IncidentStatusTransitionsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OperatorStatus_WithoutComment_Returns400()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.Acked, "operator-no-comment");

        var response = await PostAsUserAsync(
            $"/api/operator/incidents/{incidentId}/status",
            operatorToken,
            new ChangeIncidentStatusDto { ToStatus = "CANCELED", Comment = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OperatorStatus_WithComment_ChangesStatus()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.Acked, "operator-comment");

        var response = await PostAsUserAsync(
            $"/api/operator/incidents/{incidentId}/status",
            operatorToken,
            new ChangeIncidentStatusDto { ToStatus = "CANCELED", Comment = "cancel requested by client" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(payload);
        Assert.Equal("CANCELED", payload!.Status);
    }

    [Fact]
    public async Task OperatorStatus_WithClientRole_Returns403()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.Acked, "operator-forbidden");

        var response = await PostAsUserAsync(
            $"/api/operator/incidents/{incidentId}/status",
            clientToken,
            new ChangeIncidentStatusDto { ToStatus = "CANCELED", Comment = "try" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GuardAccept_FromDispatched_Works()
    {
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.Dispatched, "guard-accept-ok");

        var response = await PostAsUserAsync(
            $"/api/guard/incidents/{incidentId}/accept",
            guardToken,
            new GuardAcceptIncidentDto { Comment = "accepted by guard" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(payload);
        Assert.Equal("ACCEPTED", payload!.Status);
    }

    [Fact]
    public async Task GuardAccept_FromAcked_Returns409()
    {
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.Acked, "guard-accept-fail");

        var response = await PostAsUserAsync(
            $"/api/guard/incidents/{incidentId}/accept",
            guardToken,
            new GuardAcceptIncidentDto());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GuardProgress_ToResolved_WithoutComment_Returns400()
    {
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.OnScene, "guard-resolved-no-comment");

        var response = await PostAsUserAsync(
            $"/api/guard/incidents/{incidentId}/progress",
            guardToken,
            new GuardProgressIncidentDto { ToStatus = "RESOLVED", Comment = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GuardProgress_Sequential_Works()
    {
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.Accepted, "guard-progress");

        var enRoute = await PostAsUserAsync(
            $"/api/guard/incidents/{incidentId}/progress",
            guardToken,
            new GuardProgressIncidentDto { ToStatus = "EN_ROUTE" });
        Assert.Equal(HttpStatusCode.OK, enRoute.StatusCode);

        var onScene = await PostAsUserAsync(
            $"/api/guard/incidents/{incidentId}/progress",
            guardToken,
            new GuardProgressIncidentDto { ToStatus = "ON_SCENE" });
        Assert.Equal(HttpStatusCode.OK, onScene.StatusCode);

        var resolved = await PostAsUserAsync(
            $"/api/guard/incidents/{incidentId}/progress",
            guardToken,
            new GuardProgressIncidentDto { ToStatus = "RESOLVED", Comment = "resolved on site" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var payload = await resolved.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(payload);
        Assert.Equal("RESOLVED", payload!.Status);
    }

    [Fact]
    public async Task GuardProgress_WithOperatorRole_Returns403()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, IncidentStatus.Accepted, "guard-progress-forbidden");

        var response = await PostAsUserAsync(
            $"/api/guard/incidents/{incidentId}/progress",
            operatorToken,
            new GuardProgressIncidentDto { ToStatus = "EN_ROUTE" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private async Task<HttpResponseMessage> PostAsUserAsync<T>(string path, string token, T body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
