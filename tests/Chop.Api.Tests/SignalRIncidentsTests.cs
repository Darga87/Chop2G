using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chop.Shared.Contracts.Auth;
using Chop.Shared.Contracts.Guards;
using Chop.Shared.Contracts.Incidents;
using Chop.Shared.Contracts.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Chop.Api.Tests;

public sealed class SignalRIncidentsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public SignalRIncidentsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Hub_OperatorReceives_IncidentCreated()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");

        var tcs = new TaskCompletionSource<IncidentCreatedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateHubConnection(operatorToken);
        connection.On<IncidentCreatedEvent>("IncidentCreated", payload => tcs.TrySetResult(payload));
        await connection.StartAsync();

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(new CreateIncidentDto { AddressText = "signalr-created" }),
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        var createResponse = await _client.SendAsync(createMessage);
        createResponse.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);
        var evt = await tcs.Task;
        Assert.Equal("IncidentCreated", evt.Type);
        Assert.Equal("ACKED", evt.Incident.Status);
    }

    [Fact]
    public async Task Hub_OperatorReceives_IncidentStatusChanged()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "signalr-status");

        var tcs = new TaskCompletionSource<IncidentStatusChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateHubConnection(operatorToken);
        connection.On<IncidentStatusChangedEvent>("IncidentStatusChanged", payload =>
        {
            if (payload.IncidentId == incidentId)
            {
                tcs.TrySetResult(payload);
            }
        });
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeIncident", incidentId);

        using var statusMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/status")
        {
            Content = JsonContent.Create(new ChangeIncidentStatusDto
            {
                ToStatus = "CANCELED",
                Comment = "operator cancel",
            }),
        };
        statusMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var statusResponse = await _client.SendAsync(statusMessage);
        statusResponse.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);
        var evt = await tcs.Task;
        Assert.Equal("ACKED", evt.FromStatus);
        Assert.Equal("CANCELED", evt.ToStatus);
        Assert.Equal("OPERATOR", evt.ActorRole);
    }

    [Fact]
    public async Task Hub_ClientRole_CannotConnect()
    {
        var clientToken = await LoginAndGetAccessTokenAsync("client", "client-pass");
        await using var connection = CreateHubConnection(clientToken);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task Hub_OperatorReceives_DispatchCreated()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "signalr-dispatch");

        var tcs = new TaskCompletionSource<DispatchCreatedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateHubConnection(operatorToken);
        connection.On<DispatchCreatedEvent>("DispatchCreated", payload =>
        {
            if (payload.IncidentId == incidentId)
            {
                tcs.TrySetResult(payload);
            }
        });
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeIncident", incidentId);

        using var dispatchMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients =
                [
                    new DispatchRecipientInputDto
                    {
                        Type = "GUARD",
                        Id = TestUsers.Guard.UserId,
                    },
                ],
            }),
        };
        dispatchMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatchResponse = await _client.SendAsync(dispatchMessage);
        dispatchResponse.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);
        var evt = await tcs.Task;
        Assert.Equal("DispatchCreated", evt.Type);
        Assert.Equal(incidentId, evt.IncidentId);
    }

    [Fact]
    public async Task Hub_OperatorReceives_GuardLocationUpdated_AfterPing()
    {
        var operatorToken = await LoginAndGetAccessTokenAsync("operator", "operator-pass");
        var guardToken = await LoginAndGetAccessTokenAsync("guard", "guard-pass");
        var incidentId = await _factory.SeedIncidentAsync(TestUsers.Client.UserId, Chop.Domain.Incidents.IncidentStatus.Acked, "signalr-geo");

        var tcs = new TaskCompletionSource<GuardLocationUpdatedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateHubConnection(operatorToken);
        connection.On<GuardLocationUpdatedEvent>("GuardLocationUpdated", payload =>
        {
            if (payload.IncidentId == incidentId && payload.GuardUserId == TestUsers.Guard.UserId)
            {
                tcs.TrySetResult(payload);
            }
        });
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeIncident", incidentId);

        using var dispatchMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/operator/incidents/{incidentId}/dispatch")
        {
            Content = JsonContent.Create(new CreateDispatchDto
            {
                Method = "APP",
                Recipients =
                [
                    new DispatchRecipientInputDto
                    {
                        Type = "GUARD",
                        Id = TestUsers.Guard.UserId,
                    },
                ],
            }),
        };
        dispatchMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var dispatchResponse = await _client.SendAsync(dispatchMessage);
        dispatchResponse.EnsureSuccessStatusCode();

        using var pingMessage = new HttpRequestMessage(HttpMethod.Post, "/api/guard/location/ping")
        {
            Content = JsonContent.Create(new GuardLocationPingDto
            {
                IncidentId = incidentId,
                Lat = 55.751244,
                Lon = 37.618423,
                AccuracyM = 12.5,
            }),
        };
        pingMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardToken);
        var pingResponse = await _client.SendAsync(pingMessage);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, pingResponse.StatusCode);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);
        var evt = await tcs.Task;
        Assert.Equal("GuardLocationUpdated", evt.Type);
        Assert.Equal(incidentId, evt.IncidentId);
        Assert.Equal(TestUsers.Guard.UserId, evt.GuardUserId);
        Assert.Equal(55.751244, evt.Location.Lat);
        Assert.Equal(37.618423, evt.Location.Lon);
        Assert.Equal(12.5, evt.Location.AccuracyM);
    }

    private HubConnection CreateHubConnection(string token)
    {
        var baseUri = _factory.Server.BaseAddress;
        var hubUri = new Uri(baseUri, "/hubs/incidents");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .Build();
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
